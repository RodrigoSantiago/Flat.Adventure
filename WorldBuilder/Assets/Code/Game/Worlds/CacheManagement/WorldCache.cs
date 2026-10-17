using System;
using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Data.Queues;
using Game.Worlds.Storage;
using UnityEngine;

namespace Game.Worlds.CacheManagement {
    public class WorldCache {
        
        private WorldManager Manager { get; }
        public string Directory => cacheDirectory;
        public RegionSerialize Serializer => serializer;
        
        private readonly RegionSerialize serializer = new();
        private readonly string cacheDirectory;
        
        private readonly Dictionary<IndexPos, Region> cache = new();
        private readonly Dictionary<IndexPos, CacheWriteRequest> writeRequests = new();
        private readonly HashSet<IndexPos> storeRequests = new();

        private readonly TaskConsumer<CacheWork> consumer;

        public WorldCache(WorldManager manager) {
            Manager = manager;
            cacheDirectory = Manager.Storage + "/Cache";

            consumer = new TaskConsumer<CacheWork>("World-Cache-" + manager.Name);
            consumer.Init();
        }

        public void SetPriorityCenter(IndexPos center) {
            consumer.SortValue = center;
        }

        public void Dispose() {
            consumer.Dispose();
        }

        public void ConsumeAndDispose() {
            while (writeRequests.Count > 0) {
                writeRequests.First().Value.WaitForGpuCompletion();
            }
            consumer.CancelWhere(work => work.IsRead);
            var tasks = consumer.Dispose();
            foreach (var task in tasks) {
                try {
                    task.Execute();
                } catch {
                    // ignored
                }
            }
        }

        public Chunk LoadChunkCache(IndexPos chunkIndex, int lod) {
            var regionIndex = chunkIndex.GetChunkIndex(Region.MaxLod);
            var local = chunkIndex - regionIndex;
            int index = Region.GetLocalGroupId(lod, local.x, local.y, local.z);
            
            lock (cache) {
                if (cache.TryGetValue(regionIndex, out var region) && region.chunks[lod] != null) {
                    var chunk = region.chunks[lod][index];
                    if (chunk.CurrentVersion > 0) {
                        if (chunk.Released) {
                            chunk.Released = false;
                            region.count[lod]++;
                        }
                    }
                    return chunk;
                }
            }

            return null;
        }

        public void ReleaseChunkCache(IndexPos chunkIndex, int lod) {
            var regionIndex = chunkIndex.GetChunkIndex(Region.MaxLod);
            var local = chunkIndex - regionIndex;
            int index = Region.GetLocalGroupId(lod, local.x, local.y, local.z);
            
            lock (cache) {
                if (cache.TryGetValue(regionIndex, out var region) && region.chunks[lod] != null) {
                    var chunk = region.chunks[lod][index];
                    if (chunk.CurrentVersion > 0) {
                        if (!chunk.Released) {
                            chunk.Released = true;
                            region.count[chunk.Lod]--;
                        }
                    }

                    if (region.count[chunk.Lod] <= 0) {
                        RequestUnloadRegion(regionIndex, chunk.Lod);
                    }
                }
            }
        }

        private void RequestUnloadRegion(IndexPos regionIndex, int lod) {
            lock (cache) {
                if (!cache.TryGetValue(regionIndex, out var region) 
                    || region.chunks[lod] == null 
                    || storeRequests.Contains(regionIndex)) return;

                var updatedChunks = new List<Chunk>();
                foreach (var chunk in region.chunks[lod]) {
                    if (chunk.Modified) {
                        updatedChunks.Add(chunk);
                    }
                }

                if (updatedChunks.Count == 0) {
                    RemoveReleasedRegion(regionIndex);
                } else {
                    var writeRequest = new CacheWriteRequest(this, regionIndex, lod, updatedChunks, 
                        new TaskListener(
                            () => {
                                lock (cache) {
                                    storeRequests.Remove(regionIndex);
                                }
                            }, 
                            (e) => {
                                lock (cache) {
                                    storeRequests.Remove(regionIndex);
                                }
                                Debug.LogError(e); 
                            }));
                    writeRequests.Add(regionIndex, writeRequest);
                    storeRequests.Add(regionIndex);
                    
                    writeRequest.CreateExport();
                }
            }
        }

        public void ReleaseUnusedChunkCache() {
            lock (cache) {
                var keys = cache.Keys.Where(k => !storeRequests.Contains(k)).ToList();
                foreach (var key in keys) {
                    RemoveReleasedRegion(key);
                }
            }
        }

        public void RequestLoadRegion(IndexPos regionIndex, int lod, Action onSuccess, Action<Exception> onError) {
            consumer.Enqueue(new CacheWorkRead(this, regionIndex, 1 << lod, new TaskListener(onSuccess, onError)));
        }
        
        public void ScheduleWriteRequest(CacheWriteRequest request) {
            writeRequests.Remove(request.RegionIndex);
            consumer.Enqueue(new CacheWorkWrite(this, request.RegionIndex, request.Pairs.ToArray(), request.Listener));
        }
        
        public void PutRegion(IndexPos regionIndex, IEnumerable<Chunk> chunks) {
            lock (cache) {
                if (!cache.TryGetValue(regionIndex, out var region)) {
                    region = new Region(regionIndex);
                    cache[regionIndex] = region;
                }

                foreach (var chunk in chunks) {
                    int lod = chunk.Lod;
                    int localId = Region.GetLocalGroupId(chunk.EntryId);
                    
                    region.chunks[lod] ??= new Chunk[Region.LodSize3[lod]];
                    var prevChunk = region.chunks[lod][localId];
                    if (prevChunk == null || chunk.CurrentVersion > prevChunk.CurrentVersion) {
                        region.chunks[lod][localId] = chunk;
                    }
                }

                for (int i = 0; i < Region.TotalLods; i++) {
                    if (region.chunks[i] != null) {
                        for (int j = 0; j < region.chunks[i].Length; j++) {
                            region.chunks[i][j] ??= new Chunk(regionIndex + Region.GetLocalPosition(i, j), i, null, 0);
                        }
                    }
                }
            }
        }

        public void UpdateRegion(IndexPos regionIndex, IEnumerable<ChunkCacheUpdate> updates) {
            lock (cache) {
                if (!cache.TryGetValue(regionIndex, out var region)) {
                    region = new Region(regionIndex);
                    cache[regionIndex] = region;
                }
                
                foreach (var update in updates) {
                    int lod = Region.GetLod(update.chunkEntryId);
                    int localId = Region.GetLocalGroupId(update.chunkEntryId);
                    
                    if (region.chunks[lod] != null && region.chunks[lod][localId] != null) {
                        region.chunks[lod][localId].UpdateVersion(update);
                    }
                }

                RemoveReleasedRegion(regionIndex);
            }
        }

        private void RemoveReleasedRegion(IndexPos regionIndex) {
            lock (cache) {
                if (!cache.TryGetValue(regionIndex, out var region)) {
                    return;
                }
                
                for (int i = 0; i < Region.TotalLods; i++) {
                    if (region.chunks[i] != null && region.count[i] <= 0) {
                        for (int j = 0; j < region.chunks[i].Length; j++) {
                            var chunk = region.chunks[i][j];
                            if (chunk != null) {
                                chunk.SoilMesh?.RemoveReference();
                            }
                        }

                        region.chunks[i] = null;
                    }
                }
                
                if (region.chunks.All(chs => chs == null)) {
                    cache.Remove(regionIndex);
                }
            }
        }
    }
}