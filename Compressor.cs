using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using Microsoft;

namespace Compressor
{
    public static class Compressor
    {
        static Thread[] threadPull;
        static int partSize;
        static int maxTasksCount;

        static Queue<Task> allTasks;
        static object allTasksLock;
        static ManualResetEvent newTaskAdded; //если реализовывать специальную очередь, то событие будет в ее полномочиях
        static int newTaskAddedCounter=1;
        static object newTaskAddedCounterLocker= new object();
        static AutoResetEvent needNewTask;

        //или другая логика записи?
        static List<Task> doneTasks; //или использовать потокобезопасную  ConcurrentQueue ?... 
        static object doneTasksLock = new object();
        static ManualResetEvent newDoneTaskAdded;

        //true - закончили считывать данные из файла
        static bool endOfFile; //метод Compress выставляет в true когда заканчивает читать файл
        static object endOfFileLock = new object();

        static Compressor()
        {
            calculateSize(); //здесь?
            endOfFile = false;
            allTasks = new Queue<Task>();
            doneTasks = new List<Task>();
            threadPull = new Thread[maxTasksCount];
            newTaskAdded = new ManualResetEvent(false);
            needNewTask = new AutoResetEvent(false);
            newDoneTaskAdded = new ManualResetEvent(false);
        }

        static void calculateSize()
        {
            //???? 
            maxTasksCount = Environment.ProcessorCount*2;
            //ulong memory = Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory;//как-то по-другому можт, без VisualBasic....
            
            partSize =  100000000; 
            //          250000000
        }

        public static void Compress(string source, string destination)
        {
            Thread writeThread;
            //проверки наличия файла?
            using (FileStream sourceFile = new FileStream(source, FileMode.Open))
            {
                //создаем и запускаем потоки для сжатия
                for (int i = 0; i < maxTasksCount; i++)
                {
                    threadPull[i] = new Thread(consumerProcess);
                    threadPull[i].Name = "ConsumerProcess" + i;
                }
                foreach (Thread tr in threadPull)
                    tr.Start();

                writeThread = new Thread(new ParameterizedThreadStart(writeProcess));
                writeThread.Start(destination);

                //читаем входной файл, генерируем задания и помещаем их в очередь
                byte[] buffer  = new byte[partSize];
                int partNumb = 0;
                while (sourceFile.Read(buffer, 0, partSize) != 0)
                {
                    Console.WriteLine("Readed " + partNumb + " of " + sourceFile.Length / partSize + ". Count of tasks: " + allTasks.Count);
                    if (allTasks.Count == maxTasksCount - 1)//если очередь заданий полна
                    {
                        needNewTask.WaitOne();
                    }

                    Task newTask = new Task();
                    newTask.buffer = buffer;
                    newTask.numberOfPart = partNumb++;
                    lock (allTasksLock)
                    {
                        allTasks.Enqueue(newTask);
                        newTaskAdded.Set();
                        //newTaskAddedCounter = 1;
                    }
                }
                lock (endOfFileLock)
                {
                    endOfFile = true;
                }
                newTaskAddedCounter = 4;
                newTaskAdded.Set();// =( эт на случай если рабочийПоток успел войти в ожидание после того, как проверил endOfFile, но до того, как здесь его изменили
            }
            foreach (var t in threadPull)
                t.Join();
            if(writeThread!=null)
                writeThread.Join();
        }

        static void consumerProcess()
        {
            while (true)
            {
                Console.WriteLine(Thread.CurrentThread.Name + " заход в цикл");
                //проверяем, не пора ли заканчивать этот разврат
                //проверяем переменную конца  в локе
                bool end = false;
                lock (endOfFileLock)
                {
                    end = endOfFile; //эту конструкция наверн можно заменить с интерлок
                }
                bool needwait = false;
                lock (allTasksLock)
                {
                    //если заданий нет, но чтение еще не завершено, то ждем. иначе заканчиваем
                    if (allTasks.Count == 0)
                        if (!end)
                            needwait = true;
                        else
                            return;
                }
                if (needwait)
                {          
                    // ждем события добавления нового задания
                    newTaskAdded.WaitOne();
                    //проснулись, посчитали себя в счетчике и посмотрели, не нужно ли перезагрузить событие
                    lock (newTaskAddedCounterLocker)
                    {
                        newTaskAddedCounter++;
                        if (newTaskAddedCounter == maxTasksCount)
                        {
                            newTaskAdded.Reset();
                            newTaskAddedCounter = 0;
                        }
                    }
                    
                }

                Task task = null;
                lock (allTasksLock)
                {
                    if (allTasks.Count > 0) // опять коряво и вторая проверка.....
                    {
                        task = allTasks.Dequeue();
                        needNewTask.Set();
                    }
                }
                Console.WriteLine(Thread.CurrentThread.Name + "сейчас будем сжимать");
                //выполнение основной задачи
                if (task != null)
                {
                    Task doneTask = new Task();
                    doneTask.numberOfPart = task.numberOfPart;
                    
                    using (MemoryStream output = new MemoryStream(partSize))
                    {
                        using (GZipStream gzip = new GZipStream(output, CompressionLevel.Optimal))
                        {
                            gzip.Write(task.buffer, 0, partSize);
                            Console.WriteLine(Thread.CurrentThread.Name + "after gzip.write");
                        }
                        doneTask.buffer = output.ToArray();
                    }
                    //запись готового задания в список готовых
                    lock (doneTasksLock)
                    {
                        doneTasks.Add(doneTask);
                        newDoneTaskAdded.Set();
                    }                   
                }
                Console.WriteLine(Thread.CurrentThread.Name + "конец цикла");
            }

        }

        static void writeProcess(object destination)
        {
            String fileName = destination.ToString();
            if (File.Exists(fileName))
                File.Delete(fileName);
            using (FileStream resFile = new FileStream(fileName + ".gz", FileMode.Create, FileAccess.Write))
            {
                Task task;
                int currPartNumb = 0;

                while ((!endOfFile) || (doneTasks.Count>0) || (isAnyThreadAlive())) 
                {
                    bool needWait = false;
                    lock (doneTasksLock)
                    {
                        //если очередь пуста, или в ней нет текущей на запись части, ждем
                        if ((doneTasks.Count == 0) || (!doneTasks.Any(p => p.numberOfPart == currPartNumb)))
                        {
                            if (!isAnyThreadAlive()) /// коряво вторая проверка.....
                                needWait = true;
                            //else return;
                        }
                    }
                    if(needWait)
                        newDoneTaskAdded.WaitOne();;
 
                    lock (doneTasksLock)
                    {
                        task = doneTasks.Find(p=>(p!=null) && (p.numberOfPart==currPartNumb));
                        doneTasks.Remove(task);
                        newDoneTaskAdded.Reset();
                    }
                    if (task != null)
                    {
                        resFile.Write(task.buffer, 0, task.buffer.Length);
                        currPartNumb++;
                    }
                }
            }
        }
        static bool isAnyThreadAlive()
        {
            bool res = false;
            foreach (var t in threadPull)
            {
                if (t.IsAlive)
                    res = true;
                break;
            }
            return res;
        }

        public static void Decompress(string source, string destination)
        {

        }
    }
}
