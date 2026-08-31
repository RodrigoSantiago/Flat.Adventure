using System;
using System.Collections.Generic;
using System.Threading;
using Code;
using Game.Data;
using Game.Entities;
using Game.Worlds.Generation;
using UnityEngine;

namespace Game.Worlds {
    public class WorldManager {
        
        public IndexPos view;
        public IndexPos viewLod0;
        public IndexPos viewLod1;
        public IndexPos viewLod2;

        public string Storage { get; }
        public WorldCache Cache { get; }
        public WorldGenerator Generator { get; }
        public WorldBuilder Builder { get; }
        
        private Dictionary<IndexPos, CvChunk>[] AllChunks { get; }

        private Thread mainThread;

        public WorldManager(string storage) {
            Storage = storage;
            
            Cache = new WorldCache(this);
            Generator = new WorldGenerator(this);
            Builder = new WorldBuilder(this);
            Builder.OnChunkLoaded = OnChunkLoaded;
            
            AllChunks = new Dictionary<IndexPos, CvChunk>[Region.MaxLod];
            for (int i = 0; i < Region.MaxLod; i++) {
                AllChunks[i] = new Dictionary<IndexPos, CvChunk>();
            }
        }

        public void OnChunkLoaded(Chunk chunk) {
            if (!AllChunks[chunk.Lod].ContainsKey(chunk.Pos)) {
                var worldChunk = new GameObject("Chunk " + chunk.Pos + "[" + chunk.Lod + "]").AddComponent<CvChunk>();
                worldChunk.Setup(this, chunk);
                
                AllChunks[chunk.Lod][chunk.Pos] = worldChunk;
                for (int x = -1; x <= 1; x++) 
                for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++) {
                    var near = FindChunk(chunk.Lod, chunk.Pos + new IndexPos(x, y, z) * 32);
                    if (near != null && near != worldChunk) {
                        near.RequestMesh();
                    }
                }
            }

        }

        public CvChunk FindChunk(int lod, IndexPos pos) {
            if (AllChunks[lod].TryGetValue(pos, out var cvChunk)) {
                return cvChunk;
            }

            return null;
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