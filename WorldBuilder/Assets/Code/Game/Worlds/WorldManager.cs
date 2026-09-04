using System;
using System.Collections.Generic;
using System.IO;
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

        private bool init;
        private IndexPos prevViewLod0;
        private IndexPos prevViewLod1;
        private IndexPos prevViewLod2;

        public string Storage { get; }
        public WorldCache Cache { get; }
        public WorldGenerator Generator { get; }
        public WorldBuilder Builder { get; }

        private Dictionary<IndexPos, CvChunk>[] AllChunks { get; }

        private Thread mainThread;

        public WorldManager(string storage) {
            Storage = storage;
            if (!Directory.Exists(storage)) {
                Directory.CreateDirectory(storage);
            }

            Cache = new WorldCache(this);
            Generator = new WorldGenerator(this);
            Builder = new WorldBuilder(this);
            Builder.OnChunkLoaded = OnChunkLoaded;

            AllChunks = new Dictionary<IndexPos, CvChunk>[Region.TotalLods];
            for (int i = 0; i < Region.TotalLods; i++) {
                AllChunks[i] = new Dictionary<IndexPos, CvChunk>();
            }
        }

        public void Dispose() {
            Generator.Dispose();
            Cache.Dispose();
        }

        public void OnChunkLoaded(Chunk chunk) {
            if (!AllChunks[chunk.Lod].ContainsKey(chunk.Pos)) {
                var worldChunk = new GameObject("Chunk " + chunk.Pos + "[" + chunk.Lod + "]").AddComponent<CvChunk>();
                worldChunk.Setup(this, chunk);

                AllChunks[chunk.Lod][chunk.Pos] = worldChunk;
                for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++) {
                    var near = FindChunk(chunk.Lod, chunk.Pos + new IndexPos(x, y, z) * (32 << chunk.Lod));
                    if (near != null) {
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

        public void AddTeleportPoint(int pointId, IndexPos point) {
        }

        public void RemoveTeleportPoint(int pointId) {
        }

        public void RequestChunks() {
            if (!init) {
                init = true;
            } else {
                if (prevViewLod0 == viewLod0 && prevViewLod1 == viewLod1 && prevViewLod2 == viewLod2) {
                    return;
                }
            }
            
            prevViewLod0 = viewLod0;
            prevViewLod1 = viewLod1;
            prevViewLod2 = viewLod2;

            RequestLod(
                lod: 0,
                viewLod: viewLod0,
                chunkSize: 32,
                size: 8,
                excludeViewLod: null,
                excludeSize: 0,
                excludeChunkSize: 0
            );

            RequestLod(
                lod: 1,
                viewLod: viewLod1,
                chunkSize: 64,
                size: 12,
                excludeViewLod: viewLod0 - new IndexPos(1, 1, 1),
                excludeSize: 8 - 2,
                excludeChunkSize: 32
            );

            /*RequestLod(
                lod: 2,
                viewLod: viewLod2,
                chunkSize: 128,
                size: 16,
                excludeViewLod: viewLod1 - new IndexPos(1, 1, 1),
                excludeSize: 12 - 2,
                excludeChunkSize: 64
            );*/
        }

        private void RequestLod(
            int lod,
            IndexPos viewLod,
            int chunkSize,
            int size,
            IndexPos? excludeViewLod,
            int excludeSize,
            int excludeChunkSize
        ) {
            // +1 de contorno em cada lado.
            for (int y = -1; y <= size; y++)
            for (int z = -1; z <= size; z++)
            for (int x = -1; x <= size; x++) {

                var pos = viewLod + new IndexPos(x, y, z) * chunkSize;

                if (pos.x < 0 || pos.y < 0 || pos.z < 0) {
                    continue;
                }

                if (excludeViewLod.HasValue &&
                    IsInside(
                        pos,
                        excludeViewLod.Value,
                        excludeSize,
                        excludeChunkSize
                    )) {
                    continue;
                }

                if (!AllChunks[lod].ContainsKey(pos)) {
                    Builder.RequestChunk(pos, lod);
                }
            }
        }

        private bool IsInside(
            IndexPos pos,
            IndexPos min,
            int size,
            int chunkSize
        ) {
            var max = min + new IndexPos(
                size,
                size,
                size
            ) * chunkSize;

            return
                pos.x >= min.x && pos.x < max.x &&
                pos.y >= min.y && pos.y < max.y &&
                pos.z >= min.z && pos.z < max.z;
        }
    }
}