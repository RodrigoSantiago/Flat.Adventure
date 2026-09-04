using System;
using System.Collections.Generic;
using Game.Data;
using Game.Data.Queues;
using UnityEngine;

namespace Game.Worlds.Generation {
    public class WorldGenerator {
        
        private readonly TaskConsumer<Work> consumer;

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

        private void GenerateRegionInternal(IndexPos regionIndex) {
            Chunk[] chunks = new Chunk[Region.LodSize3[0]];
            int i = 0;
            int l = Region.LodSize1[0];
            for (int y = 0; y < l; y++)
            for (int z = 0; z < l; z++)
            for (int x = 0; x < l; x++) {
                var pos = regionIndex + (new IndexPos(x, y, z) * 32);
                var soil = GenerateSoil(pos);
                var chunk = new Chunk(pos, 0, soil) { CurrentVersion = 1 };
                chunks[i++] = chunk;
            }

            var allLods = BuildAllLodsFromBase(regionIndex, chunks);
            Cache.PutRegion(regionIndex, allLods);
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
                    
                    int maxVersion = FillLodSubBlock(lod0Chunks, subSetSoilBuffer, x, y, z, lodStep);

                    ChunkSoil soil = ChunkSoil.DownsampleFromLod0(subSetSoilBuffer, lodStep);

                    allChunks[lod][localId] = new Chunk(worldPos, lod, soil) { CurrentVersion = maxVersion };
                }
            }

            return allChunks;
        }

        private static int FillLodSubBlock(Chunk[] chunks, ChunkSoil[] targetBuffer, int lodX, int lodY, int lodZ, int lodStep) {
            int baseChunkX = lodX * lodStep;
            int baseChunkY = lodY * lodStep;
            int baseChunkZ = lodZ * lodStep;

            int maxVersion = 0;
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

        private ChunkSoil GenerateSoil(IndexPos pos) {
            byte[] mat = new byte[ChunkSoil.Size3D];
            byte[] den = new byte[ChunkSoil.Size3D];

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