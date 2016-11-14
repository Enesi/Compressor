using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Compressor
{
    public class SyncEvents
    {
        private EventWaitHandle newTaskEvent;
        private EventWaitHandle exitThreadEvent;
        private WaitHandle[] eventTaskArray;

        public SyncEvents()
        {

            newTaskEvent = new AutoResetEvent(false);
            exitThreadEvent = new ManualResetEvent(false);
            eventTaskArray = new WaitHandle[2];
            eventTaskArray[0] = newTaskEvent;
            eventTaskArray[1] = exitThreadEvent;
        }

        public EventWaitHandle ExitThreadEvent
        {
            get { return exitThreadEvent; }
        }
        public EventWaitHandle NewTaskEvent
        {
            get { return newTaskEvent; }
        }
        public WaitHandle[] EventTaskArray
        {
            get { return eventTaskArray; }
        }
    }

    public class Producer
    {
        public Producer(Queue<int> q, SyncEvents e)
        {
            _queue = q;
            _syncEvents = e;
        }
        // Producer.ThreadRun
        public void ThreadRun()
        {
            int needCount = 100;
            int count = 0;
            Random r = new Random();
            while (count<needCount)
            {
                lock (((ICollection)_queue).SyncRoot)
                {
                    while (_queue.Count < 10)
                    {
                        _queue.Enqueue(r.Next(0, 100));
                        Console.WriteLine("Producer NewItemEvent.Set");
                        _syncEvents.NewTaskEvent.Set();
                        count++;
                    }
                }
            }
            _syncEvents.ExitThreadEvent.Set();
            Console.WriteLine("Producer thread: produced {0} items", count);
        }
        private Queue<int> _queue;
        private SyncEvents _syncEvents;
    }

    public class Consumer
    {
        public Consumer(Queue<int> q, SyncEvents e)
        {
            _queue = q;
            _syncEvents = e;
        }
        // Consumer.ThreadRun
        public void ThreadRun()
        {
            bool endOfFile = false;
            int eventIndex;
            int count = 0;
            while ((eventIndex = WaitHandle.WaitAny(_syncEvents.EventTaskArray)) != -1)
            {
                Console.WriteLine(Thread.CurrentThread.Name + "awake");
                if (eventIndex == 1)
                    endOfFile = true;

                lock (((ICollection)_queue).SyncRoot)
                {
                    int item;
                    if (_queue.Count > 0)
                    {
                        Console.WriteLine(Thread.CurrentThread.Name + "took element");
                        item = _queue.Dequeue();
                    }
                    else if (endOfFile)
                    {
                        Console.WriteLine(Thread.CurrentThread.Name + "finished, consumed "+ count);
                        return;
                    }
                }
                count++;
            }
        }
        private Queue<int> _queue;
        private SyncEvents _syncEvents;
    }



    public class ThreadSyncSample
    {
        public static void ShowQueueContents(Queue<int> q)
        {
            lock (((ICollection)q).SyncRoot)
            {
                foreach (int item in q)
                {
                    Console.Write("{0} ", item);
                }
            }
            Console.WriteLine();
        }
    }
}
