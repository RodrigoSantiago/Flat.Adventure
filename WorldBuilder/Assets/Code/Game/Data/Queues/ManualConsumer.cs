using System;
using System.Collections.Generic;
using System.Threading;

namespace Game.Data.Queues {
    public class ManualConsumer<T> where T : class, ITaskGroup<T> {
        
        private readonly TaskQueue<T> queue = new();
        private string name;
        public T RunningTask { get; private set; }

        public ManualConsumer(string name) {
            this.name = name;
        }

        public IndexPos SortValue {
            get => queue.SortValue;
            set => queue.SortValue = value;
        }
        
        public void Enqueue(T task) {
            queue.Enqueue(task);
        }
        
        public void CancelWhere(Func<T, bool> predicate) {
            queue.RemoveWhere(predicate);
            if (RunningTask != null) {
                if (!RunningTask.IsCompleted && predicate.Invoke(RunningTask)) {
                    RunningTask.RequestCancel();
                }
            }
        }
        
        public void CancelAll() {
            queue.Clear();
        }

        public void Consume() {
            if (RunningTask != null) {
                if (RunningTask.IsCompleted) {
                    RunningTask = null;
                } else {
                    return;
                }
            }
            if (queue.IsEmpty) {
                return;
            }
            RunningTask = queue.Dequeue();
            RunningTask.Execute();
        }
    }
}