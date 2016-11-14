using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace Compressor
{
    public class CompressWriter
    {
        private List<Task> doneTasks;
        //private SyncEvents syncEvents;
        private String destination;

        public CompressWriter(List<Task> q, String d)
        {
            doneTasks = q;
            //syncEvents = e;
            destination = d;
        }

        public void ThreadRun()
        {
            try
            {
                if (File.Exists(destination))
                    File.Delete(destination);
                using (FileStream resFile = new FileStream(destination + ".gz", FileMode.Create, FileAccess.Write))
                {
                    Task task=null;
                    int currPartNumb = 0;

                    while((CompressManager.IsAnyConsumerThreadAlive())||(doneTasks.Count>0))
                    {
                        lock (((ICollection)doneTasks).SyncRoot)
                            if (doneTasks.Any(p => p.numberOfPart == currPartNumb))
                            {
                                task = doneTasks.Find(p => (p != null) && (p.numberOfPart == currPartNumb));
                                doneTasks.Remove(task);
                            }
                        if (task != null)
                        {
                            resFile.Write(task.buffer, 0, task.buffer.Length);
                            Console.WriteLine("Writed " + currPartNumb);
                            task = null;
                            currPartNumb++;
                        }
                    }

                    /*while ((eventIndex = WaitHandle.WaitAny(syncEvents.EventTaskArray)) != -1)
                    {
                        if (eventIndex == 1)
                        {
                            countThreadFinished++;
                            //(syncEvents.EventTaskArray[1] as ManualResetEvent).Reset();//нужно ли это делать?
                        }
                        lock (((ICollection)doneTasks).SyncRoot)
                        {
                            //если очередь выполненных заданий пуста и все сжимающие потоки завершились, завершаем работу
                            if ((doneTasks.Count == 0) && (countThreadFinished == CompressManager.MaxTasksCount))
                                return;

                            //достаем следующий кусок
                            if (doneTasks.Any(p => p.numberOfPart == currPartNumb))
                            {
                                task = doneTasks.Find(p => (p != null) && (p.numberOfPart == currPartNumb));
                                doneTasks.Remove(task);
                            }
                        }
                        if (task != null)
                        {
                            resFile.Write(task.buffer, 0, task.buffer.Length);
                            Console.WriteLine("Writed " + currPartNumb);
                            currPartNumb++;
                        }
                    }*/
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
