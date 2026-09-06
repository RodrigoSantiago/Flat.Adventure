using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Data;
using Game.Data.Queues;
using Game.Worlds.Storage;
using UnityEngine;

namespace Game.Worlds {
    public class WorldCache {
        
        private WorldManager Manager { get; }
        
        private readonly RegionSerialize serializer = new();
        private readonly string cacheDirectory;
        
        private readonly Dictionary<IndexPos, Region> cache = new();

        private readonly TaskConsumer<Work> consumer;

        private class Work : ITaskGroup<Work> {
            public WorldCache cache;

            public int Attempts { get; set; }
            
            private int requiredLods;
            private readonly IndexPos regionIndex;
            private readonly WriteStorageData writeRequest;
            private List<WorkListener> listeners;
            private int compare;

            public Work(WorldCache cache, IndexPos regionIndex, int requiredLods, WorkListener listener) {
                this.cache = cache;
                this.regionIndex = regionIndex;
                this.requiredLods = requiredLods;
                Attempts = 2;
                listeners = new List<WorkListener>{listener};
            }

            public Work(WorldCache cache, IndexPos regionIndex, WriteStorageData writeRequest) {
                this.cache = cache;
                this.regionIndex = regionIndex;
                this.writeRequest = writeRequest;
                Attempts = 2;
            }

            public void SetSortValue(IndexPos sort) {
                var d = regionIndex - sort;
                compare = d.x * d.x + d.y * d.y + d.z * d.z;
            }

            public int Compare() {
                return compare;
            }

            public bool Group(Work task) {
                if (listeners != null && task.listeners != null && task.regionIndex == regionIndex) {
                    requiredLods |= task.requiredLods;
                    listeners.AddRange(task.listeners);
                    return true;
                }

                return false;
            }

            public void Execute() {
                if (writeRequest == null) {
                    cache.ReadStoredRegion(this, regionIndex, requiredLods);
                } else {
                    cache.WriteStoredRegion(regionIndex, writeRequest);
                }
            }

            public void SetSuccess() {
                if (listeners == null) return;
                
                foreach (var listener in listeners) {
                    listener.onSuccess();
                }
            }

            public void SetError(Exception message) {
                if (listeners == null) return;
                
                foreach (var listener in listeners) {
                    listener.onError(message);
                }
            }

            public void Cancel() {
                Attempts = 0;
            }
        }
        
        private class WorkListener {
            public readonly Action onSuccess;
            public readonly Action<Exception> onError;

            public WorkListener(Action onSuccess, Action<Exception> onError) {
                this.onSuccess = onSuccess;
                this.onError = onError;
            }
        }
        
        public WorldCache(WorldManager manager) {
            Manager = manager;
            cacheDirectory = Manager.Storage;

            consumer = new TaskConsumer<Work>();
            consumer.Init();
        }

        public void SetPriorityCenter(IndexPos center) {
            consumer.SortValue = center;
        }

        public void Dispose() {
            consumer.Dispose();
        }

        // Read Region Lod from Stored File. It does not replace current data on cache
        private void ReadStoredRegion(Work task, IndexPos regionIndex, int requiredLods) {
            string path = cacheDirectory + "/" + RegionName(regionIndex) + ".region";
            if (!File.Exists(path)) {
                task.Cancel();
                throw new CacheNotFound();
            }
            
            var cacheChunks = serializer.Load(path, requiredLods);
            
            var chunks = new List<Chunk>();
            foreach (var cacheChunk in cacheChunks) {
                int id = cacheChunk.chunkEntryId;
                int lod = Region.GetLod(id);
                int local = Region.GetLocalId(id);
                var localPos = Region.GetLocalPosition(lod, local);
                chunks.Add(new Chunk(regionIndex + localPos, lod, cacheChunk));
            }

            lock (cache) {
                if (!cache.TryGetValue(regionIndex, out var region)) {
                    region = new Region(regionIndex);
                    cache[regionIndex] = region;
                }

                for (int i = 0; i < Region.TotalLods; i++) {
                    if (!Region.IsRequired(i, requiredLods) || region.chunks[i] != null) continue;
                    
                    region.chunks[i] = new Chunk[Region.LodSize3[i]];
                    for (int j = 0; j < region.chunks[i].Length; j++) {
                        var localPos = Region.GetLocalPosition(i, j);
                        region.chunks[i][j] = new Chunk(localPos, i);
                    }
                }

                for (var i = 0; i < cacheChunks.Length; i++) {
                    var cacheChunk = cacheChunks[i];
                    var chunk = chunks[i];
                    int id = cacheChunk.chunkEntryId;
                    var currentChunk = region.GetChunkByIndex(id);
                    if (currentChunk == null || chunk.Version > currentChunk.CurrentVersion) {
                        region.SetChunkByIndex(id, chunk);
                    }
                }

                cache[regionIndex] = region;
            }
        }

        // Write Region to Stored File. Remove released cache from memory
        private void WriteStoredRegion(IndexPos regionIndex, WriteStorageData request) {
            var updates = request.Updates;
            
            // Skip version already stored
            lock (cache) {
                if (cache.TryGetValue(regionIndex, out var region)) {
                    for (int i = 0; i < updates.Count; i++) {
                        var update = updates[i];
                        var chunk = region.GetChunkByIndex(update.chunkEntryId);
                        if (chunk.Version >= update.version) {
                            updates.RemoveAt(i--);
                        }
                    }
                }
            }

            if (updates.Count > 0) {
                string path = cacheDirectory + "/" + RegionName(regionIndex) + ".region";
                bool exists = File.Exists(path);
                serializer.Save(path, updates.ToArray(), exists);
            } else {
                return;
            }

            lock (cache) {
                if (cache.TryGetValue(regionIndex, out var region)) {
                    foreach (var update in updates) {
                        var chunk = region.GetChunkByIndex(update.chunkEntryId);
                        if (chunk.Version < update.version) {
                            chunk.Version = update.version;
                        }
                    }
                    
                    if (!request.Release) return;
                    
                    for (int i = 0; i < Region.TotalLods; i++) {
                        var chunks = region.chunks[i];
                        if (chunks == null) continue;
                        
                        bool released = chunks.All(chunk => chunk.Released && chunk.CurrentVersion == chunk.Version);
                        if (released) {
                            region.chunks[i] = null;
                        }
                    }
                    
                    if (region.chunks.All(chunks => chunks == null)) {
                        cache.Remove(regionIndex);
                    }
                }
            }
        }

        public Chunk LoadChunkCache(IndexPos chunkIndex, int lod) {
            var regionIndex = chunkIndex.GetChunkIndex(Region.MaxLod);
            var local = chunkIndex - regionIndex;
            int index = Region.GetLocalId(lod, local.x, local.y, local.z);
            
            lock (cache) {
                if (cache.TryGetValue(regionIndex, out var region) && region.chunks[lod] != null) {
                    var chunk = region.chunks[lod][index];
                    if (chunk.CurrentVersion > 0) {
                        chunk.Released = false;
                    }
                    return chunk;
                }
            }

            return null;
        }

        public void ReleaseChunkCache(IndexPos chunkIndex, int lod) {
            var regionIndex = chunkIndex.GetChunkIndex(Region.MaxLod);
            var local = chunkIndex - regionIndex;
            int index = Region.GetLocalId(lod, local.x, local.y, local.z);
            
            lock (cache) {
                if (cache.TryGetValue(regionIndex, out var region) && region.chunks[lod] != null) {
                    ReleaseChunkCache(region.chunks[lod][index]);
                }
            }
        }

        public void ReleaseChunkCache(Chunk chunk) {
            var regionIndex = chunk.Pos.GetChunkIndex(Region.MaxLod);
            lock (cache) {
                chunk.Released = true;
                if (cache.TryGetValue(regionIndex, out var region) && region.chunks[chunk.Lod] != null) {
                    foreach (var chunkCache in region.chunks[chunk.Lod]) {
                        if (!chunkCache.Released) {
                            return;
                        }
                    }

                    RequestUnloadRegion(regionIndex, chunk.Lod);
                }
            }
        }

        public bool IsRegionLoaded(IndexPos regionIndex, int lod) {
            lock (cache) {
                return cache.TryGetValue(regionIndex, out var region) && region.chunks[lod] != null;
            }
        }

        public void RequestUnloadRegion(IndexPos regionIndex, int lod) {
            lock (cache) {
                if (!cache.TryGetValue(regionIndex, out var region) || region.chunks[lod] == null) return;

                int count = 0;
                var current = new WriteStorageData(true);
                foreach (var chunk in region.chunks[lod]) {
                    if (chunk.Version != chunk.CurrentVersion) {
                        count++;
                        current.AddOperation(chunk.Lod, chunk.Pos);
                        chunk.RequestExport(current);
                    }
                }

                if (count == 0) {
                    
                    var chunks = region.chunks[lod];
                    if (chunks != null) {
                        bool released = chunks.All(chunk => chunk.Released);
                        if (released) {
                            region.chunks[lod] = null;
                        }
                    }

                    if (region.chunks.All(chs => chs == null)) {
                        cache.Remove(regionIndex);
                    }
                } else {
                    current.SetOnCompleted(() => consumer.Enqueue(new Work(this, regionIndex, current)));
                }
            }
        }

        public void RequestLoadRegion(IndexPos regionIndex, int lod, Action onSuccess, Action<Exception> onError) {
            consumer.Enqueue(new Work(this, regionIndex, 1 << lod, new WorkListener(onSuccess, onError)));
        }

        public void PutRegion(IndexPos regionIndex, Chunk[][] allLods) {
            lock (cache) {
                if (!cache.TryGetValue(regionIndex, out var region)) {
                    region = new Region(regionIndex);
                    cache[regionIndex] = region;
                }
                for (int i = 0; i < Region.TotalLods; i++) {
                    region.chunks[i] ??= new Chunk[Region.LodSize3[i]];
                    for (int j = 0; j < region.chunks[i].Length; j++) {
                        if (region.chunks[i][j] == null ||
                            region.chunks[i][j].CurrentVersion < allLods[i][j].CurrentVersion) {
                            region.chunks[i][j] = allLods[i][j];
                        }
                    }
                }

                var current = new WriteStorageData(false);
                foreach (var lod in allLods) {
                    foreach (var chunk in lod) {
                        current.AddOperation(chunk.Lod, chunk.Pos);
                        chunk.RequestExport(current);
                    }
                }
                current.SetOnCompleted(() => consumer.Enqueue(new Work(this, regionIndex, current)));
            }
        }

        private static string RegionName(IndexPos indexPos) {
            return indexPos.x + "_" + indexPos.y + "_" + indexPos.z;
        }
    }
}