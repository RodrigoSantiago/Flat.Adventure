using System.Collections.Generic;

namespace Game.Data.Queues {
    public class TaskQueue<T> where T : ITaskGroup<T> {

        private readonly List<T> queue = new();

        private IndexPos sortValue;

        public bool IsEmpty {
            get {
                lock (queue) {
                    return queue.Count == 0;
                }
            }
        }

        public IndexPos SortValue {
            get {
                lock (queue) {
                    return sortValue;
                }
            }
            set {
                lock (queue) {
                    sortValue = value;
                    foreach (var queueTask in queue) {
                        queueTask.SetSortValue(sortValue);
                    }
                    Sort();
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
                
                task.SetSortValue(sortValue);
                var priority = task.Compare();

                var low = 0;
                var high = queue.Count;

                while (low < high) {
                    var mid = low + (high - low) / 2;

                    if (queue[mid].Compare() <= priority) {
                        low = mid + 1;
                    }
                    else {
                        high = mid;
                    }
                }

                queue.Insert(low, task);
            }
        }

        public T Dequeue() {
            lock (queue) {
                if (queue.Count == 0) {
                    return default;
                }

                var task = queue[0];
                queue.RemoveAt(0);

                return task;
            }
        }

        public void Clear() {
            lock (queue) {
                queue.Clear();
            }
        }

        private void Sort() {
            queue.Sort((a, b) =>
                a.Compare().CompareTo(b.Compare())
            );
        }
    }
}