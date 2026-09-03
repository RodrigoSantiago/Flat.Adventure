using System;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Data.Queues {
    public class TaskConsumer<T> where T : ITaskGroup<T> {
        
        private volatile bool running;
        private Thread localThread;
        private readonly TaskQueue<T> queue = new();

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

        public void Dispose() {
            Thread joinThread;
            lock (queue) {
                if (!running) return;
                
                running = false;
                joinThread = localThread;
                localThread = null;
                
                queue.Clear();
                Monitor.PulseAll(queue);
            }

            joinThread?.Join();
        }
        
        public void Enqueue(T task) {
            lock (queue) {
                queue.Enqueue(task);
                Monitor.PulseAll(queue);
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
        
        private async void RetryLater(T task) {
            await Task.Delay(TimeSpan.FromSeconds(0.1f));
            
            lock (queue) {
                if (!running) return;
                
                queue.Enqueue(task);
                Monitor.Pulse(queue);
            }
        }
    }
}