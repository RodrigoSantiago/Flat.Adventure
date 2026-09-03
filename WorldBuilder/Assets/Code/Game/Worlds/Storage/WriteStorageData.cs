using System;
using System.Collections.Generic;
using Game.Data;

namespace Game.Worlds.Storage {
    public class WriteStorageData {

        public bool Release { get; private set; }
        private bool completed;
        private Action action;
        private List<(int, IndexPos)> Operations { get; } = new();
        private List<ChunkCacheUpdate> CompletedTasks { get; } = new();
        
        public List<ChunkCacheUpdate> Updates => new (CompletedTasks);

        public WriteStorageData(bool release) {
            Release = release;
        }

        public void AddOperation(int lod, IndexPos operation) {
            lock (Operations) {
                Operations.Add((lod, operation));
                completed = false;
            }
        }
        
        public void PutData(int lod, IndexPos operation, ChunkCacheUpdate update) {
            lock (Operations) {
                Operations.Remove((lod, operation));
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