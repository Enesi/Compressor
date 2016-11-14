using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Compressor
{
    class Program
    {
        static void Main(string[] args)
        {
            /*switch (args[0])
            {
                case "compress":
                    Compressor.Compress(args[1], args[2]);
                    break;
                case "decompress":
                    Compressor.Decompress(args[1], args[2]);
                    break;     
                default:
                    Console.WriteLine("incorrect command");
                    break;
            }*/
            //это пока для простоты
            //Compressor.Compress("source.mkv", "out");
            CompressManager.Compress("source.mkv", "out");
            Console.WriteLine("Done");
            Console.ReadLine();

            /*Queue<int> queue = new Queue<int>();
            SyncEvents syncEvents = new SyncEvents();

            Console.WriteLine("Configuring worker threads...");
            Producer producer = new Producer(queue, syncEvents);
            Consumer consumer = new Consumer(queue, syncEvents);
            Thread producerThread = new Thread(producer.ThreadRun);
            Thread[] consumerThread = new Thread[2];
            for (int i = 0; i < 2; i++)
            {
                consumerThread[i] = new Thread(consumer.ThreadRun);
                consumerThread[i].Name = "Consumer" + i;
            }

            Console.WriteLine("Launching producer and consumer threads...");
            producerThread.Start();
            for(int i=0;i<2;i++)
                consumerThread[i].Start();

            for (int i = 0; i < 1; i++)
            {
                Thread.Sleep(2500);
                ThreadSyncSample.ShowQueueContents(queue);
            }

            Console.ReadLine();
            producerThread.Join();
            for(int i=0;i<2;i++)
            consumerThread[i].Join();*/
        }
    }
}
