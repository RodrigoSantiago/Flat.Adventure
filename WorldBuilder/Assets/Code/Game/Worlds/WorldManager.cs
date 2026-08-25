using System;
using System.Collections.Generic;
using Code.Data;
using Game.Data;

namespace Code.Worlds {
    public class WorldManager {
        public static WorldManager Instance { get; } = new WorldManager();
        
        private const int MaxLOD = 3; // 0, 1, 2
        
        private Dictionary<IndexPos, WorldChunk>[] AllChunks { get; }
        public IndexPos view;
        public IndexPos viewLod0;
        public IndexPos viewLod1;
        public IndexPos viewLod2;
        
        private long LoopTick => 0;

        private ChunkMeshGenerator meshGenerator;
        private WorldBuilder builder;

        public WorldManager() {
            AllChunks = new Dictionary<IndexPos, WorldChunk>[MaxLOD];
            for (int i = 0; i < MaxLOD; i++) {
                AllChunks[i] = new Dictionary<IndexPos, WorldChunk>();
            }
        }

        public void Run(Action action) {
            // Run NOW if current thread = Unity
        }

        public void OnChunkLoaded(Chunk chunk) {
            if (!AllChunks[chunk.Lod].ContainsKey(chunk.Pos)) {
                var worldChunk = new WorldChunk(chunk);
                AllChunks[chunk.Lod][chunk.Pos] = worldChunk;
                worldChunk.Initialize();
            }

        }

        public void SetViewPoint(IndexPos point) {
            
            int chunkSize = 32;

            var center = point.GetChunkIndex(0) / chunkSize;
            var viewCenter = viewLod0 / chunkSize + 2;
            if (center.x < viewCenter.x || center.x >= viewCenter.x + 4 ||
                center.y < viewCenter.y || center.y >= viewCenter.y + 4 ||
                center.z < viewCenter.z || center.z >= viewCenter.z + 4) {
                viewCenter = center.RoundSnapTo(2) - 2;
            }
            var lod0 = viewCenter - 2;
            
            var viewCenter1 = viewLod1 / chunkSize + 4;
            if (lod0.x < viewCenter1.x || lod0.x + 8 > viewCenter1.x + 16 ||
                lod0.y < viewCenter1.y || lod0.y + 8 > viewCenter1.y + 16 ||
                lod0.z < viewCenter1.z || lod0.z + 8 > viewCenter1.z + 16) {
                viewCenter1 = center.RoundSnapTo(8) - 8;
            }
            var lod1 = viewCenter1 - 4;
            
            var viewCenter2 = viewLod2 / chunkSize + 8;
            if (lod1.x < viewCenter2.x || lod1.x + 24 > viewCenter2.x + 48 ||
                lod1.y < viewCenter2.y || lod1.y + 24 > viewCenter2.y + 48 ||
                lod1.z < viewCenter2.z || lod1.z + 24 > viewCenter2.z + 48) {
                viewCenter2 = center.RoundSnapTo(24) - 28;
            }
            var lod2 = viewCenter2 - 8;

            view = viewCenter * chunkSize;
            viewLod0 = lod0 * chunkSize;
            viewLod1 = lod1 * chunkSize;
            viewLod2 = lod2 * chunkSize;
        }

        public void RemoveViewPoint(int pointId) {
            // remove point
            // for viewPoint lod 0 >> remove (if ! isvisible)
            // for viewPoint lod 1 >> remove (if ! isvisible)
        }
    }
}