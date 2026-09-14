using System;
using System.Collections.Generic;
using System.Threading;

namespace Game.Data.Queues {
    public class TaskConsumer<T> where T : ITaskGroup<T> {
        
        private volatile bool running;
        private Thread localThread;
        private readonly TaskQueue<T> queue = new();

        public IndexPos SortValue {
            get => queue.SortValue;
            set => queue.SortValue = value;
        }

        public void Init() {
            lock (queue) {
                if (running) return;
                
                running = true;
                
                localThread = new Thread(QueueLoop) {
                    IsBackground = true
                };
                localThread.Start();
            }
        }

        public List<T> Dispose() {
            List<T> works;
            Thread joinThread;
            lock (queue) {
                if (!running) return new List<T>();
                
                running = false;
                joinThread = localThread;
                localThread = null;

                works = queue.Copy();
                queue.Clear();
                Monitor.PulseAll(queue);
            }

            joinThread?.Join();

            return works;
        }
        
        public void Enqueue(T task) {
            lock (queue) {
                queue.Enqueue(task);
                Monitor.PulseAll(queue);
            }
        }
        
        public void CancelWhere(Func<T, bool> predicate) {
            lock (queue) {
                queue.RemoveWhere(predicate);
            }
        }
        
        public void CancelAll() {
            lock (queue) {
                queue.Clear();
            }
        }

        private void QueueLoop() {
            while (running) {
                
                T runningTask;
                lock (queue) {
                    try {
                        while (running && queue.IsEmpty) {
                            Monitor.Wait(queue);
                        }
                        if (!running) return;
                        
                        runningTask = queue.Dequeue();
                    } catch {
                        continue;
                    }
                }

                try {
                    runningTask.Execute();
                    runningTask.SetSuccess();
                        
                } catch (Exception e) {
                    if (--runningTask.Attempts > 0) {
                        RetryLater(runningTask);
                    } else {
                        runningTask.SetError(e);
                    }
                }
            }
        }
        
        private void RetryLater(T task) {
            lock (queue) {
                if (!running) return;
                
                queue.Enqueue(task);
                Monitor.Pulse(queue);
            }
        }
    }
}