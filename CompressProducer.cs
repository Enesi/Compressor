using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Compressor
{
    public class CompressProducer
    {
        private Queue<Task> allTasks;
        private SyncEvents syncEvents;
        private String source;

        public CompressProducer(Queue<Task> q, SyncEvents e, String s)
        {
            allTasks = q;
            syncEvents = e;
            source = s;
        }

        public void ThreadRun()
        {
            try
            {
                using (FileStream sourceFile = new FileStream(source, FileMode.Open))
                {
                    int partNumb = 0;
                    bool endOfFile = false;
                    while (!endOfFile)
                    {                       
                        lock (((ICollection)allTasks).SyncRoot)
                        {
                            while ((allTasks.Count < CompressManager.MaxTasksCount) && (!endOfFile))
                            {
                                Task newTask = new Task();
                                newTask.buffer = new byte[CompressManager.PartSize];
                                if (sourceFile.Read(newTask.buffer, 0, CompressManager.PartSize) != 0)
                                {
                                    Console.WriteLine("Readed " + partNumb + " of " + sourceFile.Length / CompressManager.PartSize + ". Count of tasks: " + allTasks.Count);
                                    newTask.numberOfPart = partNumb++;
                                    allTasks.Enqueue(newTask);
                                    syncEvents.NewTaskEvent.Set();
                                }
                                else
                                    endOfFile = true;
                            }
                        }
                    }
                    syncEvents.ExitThreadEvent.Set();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }
    }
}
