using System.Collections.Generic;

namespace Game.Data.Queues {
    public class TaskQueue<T> where T : ITaskGroup<T> {

        private readonly Queue<T> queue = new();

        public bool IsEmpty {
            get {
                lock (queue) {
                    return queue.Count == 0;
                }
            }
        }
        
        public void Enqueue(T task) {
            lock (queue) {
                foreach (var queueTask in queue) {
                    if (queueTask.Group(task)) {
                        return;
                    }
                }

                queue.Enqueue(task);
            }
        }
        
        public T Dequeue() {
            lock (queue) {
                return queue.TryDequeue(out var task) ? task : default;
            }
        }

        public void Clear() {
            lock (queue) {
                queue.Clear();
            }
        }
    }
}