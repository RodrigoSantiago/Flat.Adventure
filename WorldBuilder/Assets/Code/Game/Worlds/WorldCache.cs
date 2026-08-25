using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Code.Data;
using Code.Worlds.Storage;
using Game.Data;

namespace Code.Worlds {
    public class WorldCache {
        
        private readonly RegionSerialize serializer = new();
        private readonly string cacheDirectory;
        
        private readonly Queue<Work> pendingQueue = new();
        private readonly Dictionary<IndexPos, Region> cache = new();

        private volatile bool running;
        private readonly Thread localThread;

        private class Work {
            public int attempts;
            public int requiredLods;
            public IndexPos regionIndex;
            public WriteStorageData writeRequest;
            public List<WorkListener> listeners;

            public void SetSuccess() {
                foreach (var listener in listeners) {
                    listener.onSuccess();
                }
            }

            public void SetError(Exception message) {
                foreach (var listener in listeners) {
                    listener.onError(message);
                }
            }
        }
        
        private class WorkListener {
            public Action onSuccess;
            public Action<Exception> onError;
        }
        
        public WorldCache(string cacheDirectory) {
            this.cacheDirectory = cacheDirectory;

            running = true;
            localThread = new Thread(QueueLoop) {
                IsBackground = true
            };
            localThread.Start();
        }

        public void Dispose() {
            running = false;
            lock (pendingQueue) {
                pendingQueue.Clear();
                Monitor.PulseAll(pendingQueue);
            }

            localThread?.Join();
        }

        private void QueueLoop() {
            while (running) {
                Work runningWork = null;
                lock (pendingQueue) {
                    try {
                        while (running && pendingQueue.Count == 0) {
                            Monitor.Wait(pendingQueue);
                        }
                        pendingQueue.TryDequeue(out runningWork);
                    } catch {
                        // ignored
                    }
                }
                
                while (running && runningWork?.attempts > 0) {
                    try {
                        if (runningWork.writeRequest != null) {
                            WriteStoredRegion(runningWork.regionIndex, runningWork.writeRequest);
                        } else {
                            ReadStoredRegion(runningWork.regionIndex, runningWork.requiredLods);
                        }
                        runningWork.SetSuccess();
                        break;
                        
                    } catch (CacheNotFound e) {
                        runningWork.SetError(e);
                        break;
                        
                    } catch {
                        if (--runningWork.attempts <= 0) {
                            runningWork.SetError(new CacheCorrupted());
                            break;
                        }
                    }
                }
            }
        }

        // Read Region Lod from Stored File. It does not replace current data on cache
        private void ReadStoredRegion(IndexPos regionIndex, int requiredLods) {
            string path = cacheDirectory + "/" + RegionName(regionIndex) + ".region";
            if (!File.Exists(path)) {
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
                    
                    region.chunks[i] = new Chunk[Region.LodSizeZ[i]];
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
            }

            lock (cache) {
                if (cache.TryGetValue(regionIndex, out var region)) {
                    foreach (var update in updates) {
                        var chunk = region.GetChunkByIndex(update.chunkEntryId);
                        if (chunk.Version < update.version) {
                            chunk.Version = update.version;
                        }
                    }
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

        public void RequestUnloadRegion(IndexPos regionIndex, int lod) {
            lock (cache) {
                if (!cache.TryGetValue(regionIndex, out var region) || region.chunks[lod] == null) return;
                
                var current = new WriteStorageData();
                foreach (var chunk in region.chunks[lod]) {
                    if (chunk.Version != chunk.CurrentVersion) {
                        chunk.RequestExport(current);
                    }
                }

                current.SetOnCompleted(() => {
                    lock (pendingQueue) {
                        var work = new Work {
                            attempts = 2,
                            requiredLods = 1 << lod,
                            regionIndex = regionIndex,
                            writeRequest = current
                        };
                        pendingQueue.Enqueue(work);
                        Monitor.PulseAll(pendingQueue);
                    }
                });
            }
        }

        public bool IsRegionLoaded(IndexPos regionIndex, int lod) {
            lock (cache) {
                return cache.TryGetValue(regionIndex, out var region) && region.chunks[lod] != null;
            }
        }

        public void RequestLoadRegion(IndexPos regionIndex, int lod, Action onSuccess, Action<Exception> onError) {
            lock (pendingQueue) {
                var current = pendingQueue.FirstOrDefault(req => req.writeRequest == null && 
                                                                 req.regionIndex == regionIndex);
                if (current == null) {
                    current = new Work {
                        attempts = 2,
                        requiredLods = 1 << lod,
                        regionIndex = regionIndex,
                        listeners = new()
                    };
                    pendingQueue.Enqueue(current);
                    Monitor.PulseAll(pendingQueue);
                }

                current.requiredLods |= 1 << lod;
                current.listeners.Add(new WorkListener {
                    onSuccess = onSuccess,
                    onError = onError
                });
            }
        }

        public void PutRegionData(IndexPos regionIndex, Chunk[] chunks, int lod, bool generateLod) {
            int cLod = lod;
            int maxLod = generateLod ? Region.MaxLod : lod; 
            while (cLod <= maxLod) {
                lock (cache) {
                    if (!cache.TryGetValue(regionIndex, out var region)) {
                        region = new Region(regionIndex);
                        cache[regionIndex] = region;
                    }

                    for (var i = 0; i < chunks.Length; i++) {
                        var chunk = chunks[i];
                        var currentChunk = region.GetChunk(cLod, i);
                        if (currentChunk == null || chunk.Version > currentChunk.CurrentVersion) {
                            region.SetChunk(cLod, i, chunk);
                        }
                    }
                }

                if (cLod == maxLod) {
                    break;
                }
                
                int dim = Region.LodSizeX[cLod];
                int nextLod = cLod + 1;
                var subLodChunk = new Chunk[Region.LodSizeZ[cLod + 1]];
                for (int z = 0; z < dim; z += 2) 
                for (int y = 0; y < dim; y += 2) 
                for (int x = 0; x < dim; x += 2) {
                    var id = Region.GetLocalId(nextLod, x / 2, y / 2, z / 2);
                    var pos = Region.GetLocalPosition(nextLod, id);
                    subLodChunk[id] = new Chunk(regionIndex + pos, nextLod, new [] {
                        chunks[LocalId(cLod, x + 0, y + 0, z + 0)], chunks[LocalId(cLod, x + 1, y + 0, z + 0)], 
                        chunks[LocalId(cLod, x + 0, y + 1, z + 0)], chunks[LocalId(cLod, x + 1, y + 1, z + 0)],
                        chunks[LocalId(cLod, x + 0, y + 0, z + 1)], chunks[LocalId(cLod, x + 1, y + 0, z + 1)], 
                        chunks[LocalId(cLod, x + 0, y + 1, z + 1)], chunks[LocalId(cLod, x + 1, y + 1, z + 1)]
                    });
                }

                chunks = subLodChunk;
                cLod++;
            }
        }

        private static int LocalId(int lod, int x, int y, int z) {
            return Region.GetLocalId(lod, x, y, z);
        }

        private static string RegionName(IndexPos indexPos) {
            return indexPos.x + "_" + indexPos.y + "_" + indexPos.z;
        }
    }
}