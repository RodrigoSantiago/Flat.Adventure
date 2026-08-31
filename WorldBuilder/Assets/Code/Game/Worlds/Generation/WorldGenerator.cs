using System;
using Game.Data;
using UnityEngine;

namespace Game.Worlds.Generation {
    public class WorldGenerator {
        private WorldManager Manager { get; }
        private WorldCache Cache => Manager.Cache;
        
        public WorldGenerator(WorldManager manager) {
            Manager = manager;
        }
        
        public void GenerateRegion(IndexPos regionIndex, Action onGenerated) {
            Chunk[] chunks = new Chunk[64];
            int i = 0;
            for (int x = 0; x < 4; x++) {
                for (int y = 0; y < 4; y++) {
                    for (int z = 0; z < 4; z++) {
                        var pos = regionIndex + (new IndexPos(x, y, z) * 32);
                        var soil = GenerateSoil(pos);
                        var chunk = new Chunk(pos, 0, soil);
                        chunk.CurrentVersion = 1;
                        chunks[i++] = chunk;
                    }
                }
            }

            var allLods = BuildLods(regionIndex, chunks);
            Cache.PutRegion(regionIndex, allLods);
            onGenerated.Invoke();
        }

        public Chunk[][] BuildLods(IndexPos regionIndex, Chunk[] chunks) {
            Chunk[][] allChunks = new Chunk[3][];
            allChunks[0] = chunks;
            
            int cLod = 0;
            while (cLod < Region.MaxLod) {
                int dim = Region.LodSizeX[cLod];
                int nextLod = cLod + 1;
                
                allChunks[nextLod] = new Chunk[Region.LodSizeZ[nextLod]];
                for (int z = 0; z < dim; z += 2) 
                for (int y = 0; y < dim; y += 2) 
                for (int x = 0; x < dim; x += 2) {
                    var id = Region.GetLocalId(nextLod, x / 2, y / 2, z / 2);
                    var pos = Region.GetLocalPosition(nextLod, id);
                    allChunks[nextLod][id] = new Chunk(regionIndex + pos, nextLod, new [] {
                        chunks[LocalId(cLod, x + 0, y + 0, z + 0)], chunks[LocalId(cLod, x + 1, y + 0, z + 0)], 
                        chunks[LocalId(cLod, x + 0, y + 0, z + 1)], chunks[LocalId(cLod, x + 1, y + 0, z + 1)],
                        chunks[LocalId(cLod, x + 0, y + 1, z + 0)], chunks[LocalId(cLod, x + 1, y + 1, z + 0)], 
                        chunks[LocalId(cLod, x + 0, y + 1, z + 1)], chunks[LocalId(cLod, x + 1, y + 1, z + 1)]
                    });
                }

                chunks = allChunks[nextLod];
                cLod++; 
            }
            return allChunks;
        }
        
        private static int LocalId(int lod, int x, int y, int z) {
            return Region.GetLocalId(lod, x, y, z);
        }

        private ChunkSoil GenerateSoil(IndexPos pos) {
            var soil = new ChunkSoil();
            for (int x = 0; x < 32; x++) {
                for (int y = 0; y < 32; y++) {
                    for (int z = 0; z < 32; z++) {
                        var p = pos + new IndexPos(x, y, z);
                        if (p.y - 4 <= p.x) {
                            soil.SetDensity(x, y, z, 1);
                            soil.SetMaterial(x, y, z, 1);
                        } else {
                            soil.SetDensity(x, y, z, 0);
                            soil.SetMaterial(x, y, z, 0);
                        }
                    }
                }
            }

            return soil;
        }

        public static ChunkSoil GenerateTest() {
            var chunk = new ChunkSoil();
            
            float radius = ChunkSoil.Size1D * 0.4f;
            Vector3 center = new(
                ChunkSoil.Size1D * 0.5f,
                ChunkSoil.Size1D * 0.5f,
                ChunkSoil.Size1D * 0.5f
            );
            const float aa = 1.0f;
            for (int y = 0; y < ChunkSoil.Size1D; y++) {
                for (int z = 0; z < ChunkSoil.Size1D; z++) {
                    for (int x = 0; x < ChunkSoil.Size1D; x++) {

                        Vector3 p = new(x, y, z);

                        float sdf = (p - center).magnitude - radius;

                        float density = Mathf.Clamp01(
                            0.5f - sdf / (aa * 2.0f)
                        );

                        chunk.SetDensity(x, y, z, density);
                    }
                }
            }
            
            const int baseMargin = 2;
            const int floorHeight = 5;

            for (int y = 0; y < ChunkSoil.Size1D; y++) {
                for (int z = 0; z < ChunkSoil.Size1D; z++) {
                    for (int x = 0; x < ChunkSoil.Size1D; x++) {

                        float density = 0.0f;

                        if (y >= baseMargin && y < baseMargin + floorHeight) {

                            int layer = y - baseMargin; // 0..4

                            int currentMargin = baseMargin + layer;

                            bool inside =
                                x >= currentMargin &&
                                x < ChunkSoil.Size1D - currentMargin &&
                                z >= currentMargin &&
                                z < ChunkSoil.Size1D - currentMargin;

                            if (inside)
                                density = 1.0f;
                        }
                        if (density != 0)
                            chunk.SetDensity(x, y, z, density);
                    }
                }
            }
            for (int y = 0; y < 8; y++) {
                for (int z = 0; z < ChunkSoil.Size1D; z++) {
                    for (int x = 0; x < ChunkSoil.Size1D; x++) {
                        chunk.SetMaterial(x, y, z, 1);
                    }
                }
            }
            
            for (int y = 0; y < 3; y++) {
                for (int z = 0; z < ChunkSoil.Size1D; z++) {
                    for (int x = 0; x < ChunkSoil.Size1D; x++) {
                        chunk.SetDensity(x, y, z, 1.0f);
                    }
                }
            }

            return chunk;
        }
    }
}