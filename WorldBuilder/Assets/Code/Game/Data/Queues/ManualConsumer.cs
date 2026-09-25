using System;
using System.Collections.Generic;
using System.Threading;

namespace Game.Data.Queues {
    public class ManualConsumer<T> where T : ITaskGroup<T> {
        
        private readonly TaskQueue<T> queue = new();
        private string name;
        private T runningTask;

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
        }
        
        public void CancelAll() {
            queue.Clear();
        }

        public void Consume() {
            if (queue.IsEmpty || (runningTask != null && !runningTask.IsCompleted)) return;
            runningTask = queue.Dequeue();
            runningTask.Execute();
        }
    }
}