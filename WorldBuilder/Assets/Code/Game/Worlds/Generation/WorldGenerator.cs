using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Game.Data;
using Game.Data.Queues;
using Game.Worlds.Storage;
using UnityEngine;
using WorldCache = Game.Worlds.CacheManagement.WorldCache;

namespace Game.Worlds.Generation {
    public class WorldGenerator {
        
        private readonly TaskConsumer<Work> consumer;
        private readonly RegionSerialize serializer = new();

        private WorldManager Manager { get; }
        private WorldCache Cache => Manager.Cache;
        
        private class Work : ITaskGroup<Work> {
            public int Attempts { get; set; }
            
            private WorldGenerator generator;
            private IndexPos regionIndex;
            private List<Action> listeners = new();
            private int compare;

            public Work(WorldGenerator generator, IndexPos regionIndex, Action listener) {
                this.generator = generator;
                this.regionIndex = regionIndex;
                listeners.Add(listener);
            }

            public void SetSortValue(IndexPos sort) {
                var d = regionIndex - sort;
                compare = d.x * d.x + d.y * d.y + d.z * d.z;
            }

            public int Compare() {
                return compare;
            }

            public bool Group(Work task) {
                if (task.regionIndex == regionIndex) {
                    listeners.AddRange(task.listeners);
                    return true;
                }

                return false;
            }

            public void Execute() {
                generator.GenerateRegionInternal(regionIndex);
            }

            public void SetSuccess() {
                foreach (var listener in listeners) {
                    listener.Invoke();
                }
            }

            public void SetError(Exception e) {
                Debug.LogError("Failed to generate chunk [" + regionIndex + "]: " + e);
            }
        }

        public WorldGenerator(WorldManager manager) {
            Manager = manager;
            consumer = new TaskConsumer<Work>();
            consumer.Init();
        }

        public void SetPriorityCenter(IndexPos center) {
            consumer.SortValue = center;
        }

        public void Dispose() {
            consumer.Dispose();
        }

        public void EnqueueRegion(IndexPos regionIndex, Action onGenerated) {
            consumer.Enqueue(new Work(this, regionIndex, onGenerated));
        }

        [ThreadStatic] private static byte[] t_matBuffer;
        [ThreadStatic] private static byte[] t_denBuffer;

        private void GenerateRegionInternal(IndexPos regionIndex) {
            int totalLod0Chunks = Region.LodSize3[0];
            int gridDimL0 = Region.LodSize1[0];
            var lod0Chunks = new Chunk[totalLod0Chunks];
            
            Parallel.For(0, gridDimL0, y => {
                t_matBuffer ??= new byte[ChunkSoil.Size3D];
                t_denBuffer ??= new byte[ChunkSoil.Size3D];

                for (int z = 0; z < gridDimL0; z++) {
                    for (int x = 0; x < gridDimL0; x++) {
                        int index = x + (z * gridDimL0) + (y * gridDimL0 * gridDimL0);
                        
                        var pos = new IndexPos(
                            regionIndex.x + (x * 32),
                            regionIndex.y + (y * 32),
                            regionIndex.z + (z * 32)
                        );

                        var soil = GenerateSoil(pos, t_matBuffer, t_denBuffer);
                        lod0Chunks[index] = new Chunk(pos, 0, soil, 1);
                    }
                }
            });

            var allLods = BuildAllLodsFromBase(regionIndex, lod0Chunks);
            
            Cache.PutRegion(regionIndex, allLods.SelectMany(lod => lod));
        }
        
        private void ExportToFile(IndexPos regionIndex, Chunk[][] allLods) {
            string path = Manager.Storage + "/" + Region.GenName(regionIndex) + ".region";
            bool exists = File.Exists(path);
            
            var updates = new List<ChunkCacheUpdate>();
            
            foreach (var lod in allLods) {
                foreach (var chunk in lod) {
                    var update = new ChunkCacheUpdate(chunk.EntryId);
                    chunk.ExportData(update);
                    updates.Add(update);
                }
            }

            serializer.Save(path, updates.ToArray(), exists);
        }
        
        private Chunk[][] BuildAllLodsFromBase(IndexPos regionIndex, Chunk[] lod0Chunks) {
            var allChunks = new Chunk[Region.TotalLods][];
            allChunks[0] = lod0Chunks;
            
            var subSetSoilBuffer = new ChunkSoil[512];
            
            for (int lod = 1; lod <= Region.MaxLod; lod++) {
                int lodStep = 1 << lod; // 2, 4, 8
                int gridDim = Region.LodSize1[lod]; // 4, 2, 1
                int totalLodChunks = Region.LodSize3[lod]; // 64, 8, 1

                allChunks[lod] = new Chunk[totalLodChunks];

                for (int y = 0; y < gridDim; y++) 
                for (int z = 0; z < gridDim; z++) 
                for (int x = 0; x < gridDim; x++) {
            
                    int localId = x + (z * gridDim) + (y * gridDim * gridDim);
                    IndexPos worldPos = regionIndex + Region.GetLocalPosition(lod, localId);
                    
                    long maxVersion = FillLodSubBlock(lod0Chunks, subSetSoilBuffer, x, y, z, lodStep);

                    ChunkSoil soil = ChunkSoil.DownsampleFromLod0(subSetSoilBuffer, lodStep);

                    allChunks[lod][localId] = new Chunk(worldPos, lod, soil, maxVersion);
                }
            }

            return allChunks;
        }

        private static long FillLodSubBlock(Chunk[] chunks, ChunkSoil[] targetBuffer, int lodX, int lodY, int lodZ, int lodStep) {
            int baseChunkX = lodX * lodStep;
            int baseChunkY = lodY * lodStep;
            int baseChunkZ = lodZ * lodStep;

            long maxVersion = 0;
            int idx = 0;

            for (int cy = 0; cy < lodStep; cy++)
            for (int cz = 0; cz < lodStep; cz++)
            for (int cx = 0; cx < lodStep; cx++) {
                int lod0Index = (baseChunkX + cx) | ((baseChunkZ + cz) << 3) | ((baseChunkY + cy) << 6);
        
                var chunk = chunks[lod0Index];
                targetBuffer[idx++] = chunk.Soil;

                if (chunk.CurrentVersion > maxVersion) {
                    maxVersion = chunk.CurrentVersion;
                }
            }

            return maxVersion;
        }
        
        private ChunkSoil GenerateSoil(IndexPos pos, byte[] mat, byte[] den) {

            byte[] palette = {0, 1};
            int n = 0;
            for (int y = 0; y < 32; y++) 
            for (int z = 0; z < 32; z++)
            for (int x = 0; x < 32; x++) {
                var p = pos + new IndexPos(x, y, z);
                if (x >= 8 && x < 24 && y >= 8 && y < 24 && z >= 8 && z < 24) {
                    den[n] = 14;
                    mat[n] = 1;
                } else {
                    den[n] = 0;
                    mat[n] = 0;
                }
                n++;
            }

            return ChunkSoil.CreateFromRaw(mat, den, palette, 2);
        }
    }
}