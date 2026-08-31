using System;
using System.Collections.Generic;
using Game.Data;

namespace Game.Worlds.Storage {
    public class WriteStorageData {

        private bool completed;
        private Action action;
        private List<IndexPos> Operations { get; } = new();
        private List<ChunkCacheUpdate> CompletedTasks { get; } = new();
        
        public List<ChunkCacheUpdate> Updates => new (CompletedTasks);

        public void AddOperation(IndexPos operation) {
            lock (Operations) {
                Operations.Add(operation);
                completed = false;
            }
        }
        
        public void PutData(IndexPos operation, ChunkCacheUpdate update) {
            lock (Operations) {
                Operations.Remove(operation);
                CompletedTasks.Add(update);
                completed = (Operations.Count == 0);

                if (completed && action != null) {
                    action.Invoke();
                }
            }
        }

        public void SetOnCompleted(Action action) {
            lock (Operations) {
                this.action = action;
                if (completed) {
                    action.Invoke();
                }
            }
        }
    }
}