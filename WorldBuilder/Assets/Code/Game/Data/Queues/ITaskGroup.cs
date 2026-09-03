using System;

namespace Game.Data.Queues {
    public interface ITaskGroup<T> where T : ITaskGroup<T> {
        public int Attempts { get; set; }
        
        public bool Group(T task);
        public void Execute();
        public void SetSuccess();
        public void SetError(Exception e);
    }
}