using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Compressor
{
    public class Task
    {
        public byte[] buffer;
        public int numberOfPart;
    }

    public static class CompressManager
    {
        static Thread writeThread;
        static Thread readThread;
        static Thread[] threadPull;
        static int partSize;
        static int maxTasksCount;

        static Queue<Task> allTasks = new Queue<Task>();
        static List<Task> doneTasks = new List<Task>();

        static SyncEvents syncProConsEvents;
        static SyncEvents syncConsWriteEvents;

        public static int PartSize { get { return partSize; } }
        public static int MaxTasksCount { get { return maxTasksCount; } }

        static CompressManager()
        {
            //инициализация
            calculateSize();
            threadPull = new Thread[maxTasksCount];
            syncProConsEvents = new SyncEvents();
            syncConsWriteEvents = new SyncEvents();
        }

        static void calculateSize()
        {
            //???? 
            maxTasksCount = Environment.ProcessorCount * 2;
            //ulong memory = Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory;//как-то по-другому можт, без VisualBasic....

            partSize = 100000000;
            //         250000000
        }

        public static bool IsAnyConsumerThreadAlive()
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

        public static void Compress(string source, string destination)
        {
            CompressProducer producer = new CompressProducer(allTasks, syncProConsEvents, source);
            CompressConsumer consumer = new CompressConsumer(allTasks, doneTasks, syncProConsEvents,syncConsWriteEvents);
            CompressWriter writer = new CompressWriter(doneTasks,destination);
            //создаем и запускаем поток для чтения
            readThread = new Thread(producer.ThreadRun);
            readThread.Start();
            //создаем и запускаем потоки для сжатия
            for (int i = 0; i < maxTasksCount; i++)
            {
                threadPull[i] = new Thread(consumer.ThreadRun);
                threadPull[i].Name = "ConsumerProcess" + i;
            }
            foreach (Thread tr in threadPull)
                tr.Start();
            //создаем и запускаем поток для записи 
            writeThread = new Thread(writer.ThreadRun);
            writeThread.Start();

            //синхронизируем
            readThread.Join();
            foreach (var t in threadPull)
                t.Join();
            writeThread.Join();
        }
    }
}
