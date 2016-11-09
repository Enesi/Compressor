using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;

namespace Compressor
{
    public struct Task 
    {
        public byte[] buffer;
        public int numberOfPart;
    }

    public static class Compressor
    {
        static Thread[] threadPull;

        static Queue<Task> allTasks; //или использовать потокобезопасную  ConcurrentQueue ?... 
        static object allTasksLock = new object();
        static int partSize;
        static int maxTasksCount;
        static ManualResetEvent newTaskAdded; //если реализовывать специальную очередь, то событие будет в ее полномочиях
        static ManualResetEvent needNewTask;

        //или другая логика записи?
        static List<Task> doneTasks; //или использовать потокобезопасную  ConcurrentQueue ?... 
        static object doneTasksLock = new object();
        static ManualResetEvent newDoneTaskAdded;

        //true - закончили считывать данные из файла
        static bool endOfFile;

        static Compressor()
        {
            calculateSize(); //здесь?
            endOfFile = false;
            allTasks = new Queue<Task>();
            doneTasks = new List<Task>();
            threadPull = new Thread[maxTasksCount];
            newTaskAdded = new ManualResetEvent(false);
            needNewTask = new ManualResetEvent(false);
            newDoneTaskAdded = new ManualResetEvent(false);
        }

        static void calculateSize()
        {
            //???? 
            maxTasksCount = 1;
            partSize = Environment.SystemPageSize/(maxTasksCount*2); 
        }

        public static void Compress(string source, string destination)
        {
            //проверки наличия файла?
            using (FileStream sourceFile = new FileStream(source, FileMode.Open))
            {
                //содздаем и запускаем потоки для сжатия
                for (int i = 0; i < maxTasksCount; i++)
                    threadPull[i] = new Thread(consumerProcess);
                foreach (Thread tr in threadPull)
                    tr.Start();

                Thread writeThread = new Thread(new ParameterizedThreadStart(writeProcess));
                writeThread.Start(destination);

                //читаем входной файл, генерируем задания и помещаем их в очередь
                byte[] buffer  = new byte[partSize];
                int partNumb = 0;
                while (sourceFile.Read(buffer, 0, partSize) != 0)
                {
                    if (allTasks.Count == maxTasksCount)
                    {
                        needNewTask.WaitOne();
                        needNewTask.Reset();
                    }

                    lock (allTasksLock)
                    {
                        Task newTask = new Task();
                        newTask.buffer = buffer;
                        newTask.numberOfPart = partNumb++;
                        allTasks.Enqueue(newTask);
                        newTaskAdded.Set();
                    }
                }
                endOfFile = true;
            }
            foreach (var t in threadPull)
                t.Join();
        }

        static void consumerProcess()
        {
            while (!endOfFile)
            {
                Task task;
                // если очередь заданий пуста, ждем события добавления нового задания
                lock (allTasksLock)
                {
                    if (allTasks.Count == 0)
                    {
                        Monitor.Pulse(allTasksLock);
                        Monitor.Wait(allTasksLock);
                        newTaskAdded.WaitOne();
                        newTaskAdded.Reset();
                    }

                    task = allTasks.Dequeue();
                    needNewTask.Set();
                }
                Task doneTask = new Task();
                doneTask.numberOfPart = task.numberOfPart;
                using (MemoryStream output = new MemoryStream(partSize))
                {
                    using (GZipStream gzip = new GZipStream(output, CompressionMode.Compress))
                    {
                        gzip.Write(task.buffer, 0, partSize);
                    }
                    doneTask.buffer = output.ToArray();
                }

                lock (doneTasksLock)
                {
                    doneTasks.Add(doneTask);
                    newDoneTaskAdded.Set();
                }

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

                while (!endOfFile) //еще надо проверять, работают ли еще потоки по сжатию
                {
                    //если очередь пуста, или в ней нет текущей на запись части, ждем
                    if ((doneTasks.Count == 0)||(!doneTasks.Any(p=>p.numberOfPart==currPartNumb)))
                        newDoneTaskAdded.WaitOne();
 
                    lock (doneTasks)
                    {
                        task = doneTasks.Find(p=>p.numberOfPart==currPartNumb);
                        doneTasks.Remove(task);
                        newDoneTaskAdded.Reset();
                    }
                    resFile.Write(task.buffer, 0, task.buffer.Length);
                    currPartNumb++;
                }
            }
        }

        public static void Decompress(string source, string destination)
        {

        }
    }
}
