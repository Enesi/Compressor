using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Compression;
using System.IO;

namespace Compressor
{
    public class CompressConsumer
    {
        private Queue<Task> allTasks;
        private List<Task> doneTasks;
        private SyncEvents syncProConsEvents;
        private SyncEvents syncConsWriteEvents;

        public CompressConsumer(Queue<Task> q, List<Task> d, SyncEvents eProCons, SyncEvents eConsWrite)
        {
            allTasks = q;
            doneTasks = d;
            syncProConsEvents = eProCons;
            syncConsWriteEvents = eConsWrite;
        }

        public void ThreadRun()
        {
            bool endOfFile = false;
            int eventIndex;
            while ( (eventIndex = WaitHandle.WaitAny(syncProConsEvents.EventTaskArray)) != -1)
            {
                //если получаем сигнал об окончании работы считывабщего потока, запоминаем, что он закончил работу
                if (eventIndex==1)
                    endOfFile=true;

                //получение задания
                Task task = null;
                lock (((ICollection)allTasks).SyncRoot)
                {
                    if (allTasks.Count > 0)
                        task = allTasks.Dequeue();
                    else if (endOfFile) //если очередь пуста и считывающий поток уже закончил свое выполнение, заканчиваем работу
                    {
                        syncConsWriteEvents.ExitThreadEvent.Set();
                        return;
                    }
                }

                //выполнение задания
                if (task != null)
                {
                    Console.WriteLine(Thread.CurrentThread.Name + " doing task");
                    Task doneTask = new Task();
                    doneTask.numberOfPart = task.numberOfPart;

                    using (MemoryStream output = new MemoryStream(CompressManager.PartSize))
                    {
                        using (GZipStream gzip = new GZipStream(output, CompressionMode.Compress))
                        {
                            gzip.Write(task.buffer, 0, CompressManager.PartSize);
                        }
                        doneTask.buffer = output.ToArray();
                    }
                    //запись готового задания в список готовых
                    lock (((ICollection)doneTasks).SyncRoot)
                    {
                        doneTasks.Add(doneTask);
                        syncConsWriteEvents.NewTaskEvent.Set();
                    }
                }
            }
        }
    }
}
