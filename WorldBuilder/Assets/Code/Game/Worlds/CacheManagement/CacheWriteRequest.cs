using System;
using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Data.Queues;
using UnityEngine.Rendering;

namespace Game.Worlds.CacheManagement {
    public class CacheWriteRequest {
        private readonly WorldCache cache;

        public IndexPos RegionIndex { get; }
        public int Lod { get; }

        public List<ChunkCachePair> Pairs { get; } = new();
        public TaskListener Listener { get; }
        
        private List<Chunk> Chunks { get; }
        private List<AsyncGPUReadbackRequest> GpuTasks { get; } = new();
        private bool creating;
        private bool scheduled;

        public CacheWriteRequest(WorldCache cache, IndexPos regionIndex, int lod, List<Chunk> chunks, TaskListener listener) {
            this.cache = cache;
            RegionIndex = regionIndex;
            Lod = lod;
            Listener = listener;
            Chunks = chunks;
        }

        private bool IsGpuRequestDone => GpuTasks.Count == 0 || GpuTasks.All(task => task.done);

        private void RefreshTasks() {
            if (creating) return;
            if (IsGpuRequestDone) {
                ScheduleTask();
            }
        }

        private void ScheduleTask() {
            if (scheduled) return;
            
            scheduled = true;
            cache.ScheduleWriteRequest(this);
        }

        public void WaitForGpuCompletion() {
            foreach (var task in GpuTasks) {
                if (!task.done) {
                    task.WaitForCompletion();
                }
            }

            ScheduleTask();
        }

        public void CreateExport() {
            creating = true;
            
            foreach (var chunk in Chunks) {
                var update = new ChunkCacheUpdate(chunk.EntryId);
                
                if (chunk.RequestMeshAsync((ver, data) => {
                        update.meshVersion = ver;
                        update.meshData = data;
                        RefreshTasks();
                    }, out var request)) {
                    GpuTasks.Add(request);
                }

                Pairs.Add(new ChunkCachePair(chunk, update));
            }
            
            creating = false;
        }
    }
}