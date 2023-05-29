using System.Collections.Generic;
using System.Threading;
using Adventure.Logic.ChunkManagment;
using Adventure.Logic.Data;
using UnityEngine;

namespace Adventure.Logic.Generator {
    public class WorldGenerator {

        private Dictionary<Vector3Int, BlockLine> cacheLines = new Dictionary<Vector3Int, BlockLine>();
        
        private readonly World world;
        private readonly WorldMap worldMap;
        
        private readonly Queue<ChunkRequest> request = new();
        private readonly Queue<ChunkRequest> done = new();
        private readonly Thread thread;

        public WorldGenerator(World world, WorldMap worldMap) {
            this.world = world;
            this.worldMap = worldMap;

            thread = new Thread(Loop);
            thread.Start();
        }

        public void AddChunkRequest(ChunkRequest chunkRequest) {
            lock (request) {
                request.Enqueue(chunkRequest);
                Monitor.PulseAll(request);
            }
        }

        public void Loop() {
            while (world.alive) {
                ChunkRequest chunkRequest;
                lock (request) {
                    while (request.Count == 0) {
                        Monitor.Wait(request);
                    }

                    chunkRequest = request.Dequeue();
                }

                if (chunkRequest.lod == 0) {
                    chunkRequest.AddChunk(GenerateChunk(chunkRequest.local));
                } else {
                    chunkRequest.PackChunks();
                }
                lock (done) {
                    done.Enqueue(chunkRequest);
                }
            }
        }

        public ChunkRequest Read() {
            lock (done) {
                return done.Count == 0 ? null : done.Dequeue();
            }
        }

        private readonly Voxel[] voxels = new Voxel[16 * 16 * 16];
        
        public Chunk GenerateChunk(Vector3Int local) {
            
            bool single = true;
            Voxel baseVoxel = new Voxel();
            for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++) { 
                var h = worldMap.getHeight(local.x + x, local.z + z);
                
                for (int y = 0; y < 16; y++) {
                    float m2 = (h - (local.y + y));
                    var voxel = new Voxel(m2 / 2 + 0.5f, 2);
                    voxels[x + y * 16 + z * 256] = voxel;
                    if (x == 0 && y == 0 && z == 0) {
                        baseVoxel = voxel;
                    } else if (baseVoxel != voxel) {
                        single = false;
                    }
                }
            }

            return single ? new Chunk(local, 0, true, baseVoxel) : new Chunk(local, 0, true, voxels);
        }
    }
}