using System;

namespace Game.Data.Queues {
    public class TaskListener {
        public Action OnSuccess { get; }
        public Action<Exception> OnError { get; }

        public TaskListener(Action onSuccess, Action<Exception> onError) {
            OnSuccess = onSuccess;
            OnError = onError;
        }
    }
    public class TaskListener<T> {
        public Action<T> OnSuccess { get; }
        public Action<Exception> OnError { get; }

        public TaskListener(Action<T> onSuccess, Action<Exception> onError) {
            OnSuccess = onSuccess;
            OnError = onError;
        }
    }
}