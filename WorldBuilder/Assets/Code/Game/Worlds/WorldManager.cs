using System.Collections.Generic;
using System.IO;
using System.Threading;
using Game.Data;
using Game.Worlds.Generation;
using Game.Worlds.Rendering;

namespace Game.Worlds {
    public class WorldManager {

        public IndexPos centerPoint;
        public IndexPos view;
        public IndexPos viewLod0;
        public IndexPos viewLod1;
        public IndexPos viewLod2;

        private bool init;
        private IndexPos prevCenter;
        private IndexPos prevViewLod0;
        private IndexPos prevViewLod1;
        private IndexPos prevViewLod2;

        public string Storage { get; }
        public WorldCache Cache { get; }
        public WorldGenerator Generator { get; }
        public WorldBuilder Builder { get; }
        public WorldRender Render { get; }

        private Dictionary<IndexPos, ChunkRenderData>[] AllChunksData { get; }

        private readonly HashSet<IndexPos>[] requestedPositions;
        private readonly List<IndexPos> toRemoveList = new ();

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

            AllChunksData = new Dictionary<IndexPos, ChunkRenderData>[Region.TotalLods];
            requestedPositions = new HashSet<IndexPos>[Region.TotalLods];

            for (int i = 0; i < Region.TotalLods; i++) {
                AllChunksData[i] = new Dictionary<IndexPos, ChunkRenderData>();
                requestedPositions[i] = new HashSet<IndexPos>();
            }
            
            Render = new WorldRender(this);
        }

        public void Dispose() {
            Generator.Dispose();
            Cache.Dispose();
        }

        public void OnChunkLoaded(Chunk chunk) {
            if (!AllChunksData[chunk.Lod].TryGetValue(chunk.Pos, out var chunkData)) {
                chunkData = new ChunkRenderData(this, chunk);
                AllChunksData[chunk.Lod][chunk.Pos] = chunkData;
            } else {
                chunkData.RefreshChunk(chunk);
            }
                
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++) {
                var near = FindChunkData(chunk.Lod, chunk.Pos + new IndexPos(x, y, z) * (32 << chunk.Lod));
                if (near != null) {
                    near.RequestMesh();
                }
            }
        }

        public Chunk FindChunk(int lod, IndexPos pos) {
            return FindChunkData(lod, pos)?.Chunk;
        }

        public ChunkRenderData FindChunkData(int lod, IndexPos pos) {
            if (AllChunksData[lod].TryGetValue(pos, out var cvChunk)) {
                return cvChunk;
            }

            return null;
        }

        public void SetViewPoint(IndexPos point) {
            centerPoint = point;
            
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
        
        public void RenderChunks() {
            Render.RenderFrame();
        }

        public void RequestChunk(int lod, IndexPos chunkIndex) {
            Builder.RequestChunk(chunkIndex, lod);
        }

        public void RequestChunks() {
            if (!init) {
                init = true;
            } else {
                if (prevViewLod0 == viewLod0 && prevViewLod1 == viewLod1 && prevViewLod2 == viewLod2) {
                    return;
                }
            }

            if (prevCenter != centerPoint) {
                Cache.SetPriorityCenter(centerPoint);
                Generator.SetPriorityCenter(centerPoint);
            }
            
            prevCenter = centerPoint;
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
                excludeViewLod: viewLod0 + new IndexPos(32, 32, 32),
                excludeSize: 8 - 4,
                excludeChunkSize: 32
            );

            RequestLod(
                lod: 2,
                viewLod: viewLod2,
                chunkSize: 128,
                size: 16,
                excludeViewLod: viewLod1 + new IndexPos(64, 64, 64),
                excludeSize: 12 - 4,
                excludeChunkSize: 64
            );

            UnloadNotRequiredChunks();

            for (int i = 0; i < Region.TotalLods; i++) {
                requestedPositions[i].Clear();
            }
        }

        public bool IsChunkRenderRequired(int lod, IndexPos pos) {
            if (lod == 0) {
                return IsInside(pos, viewLod0, 8, 32);
            } else if (lod == 1) {
                return IsInside(pos, viewLod1, 12, 64) && !IsInside(pos, viewLod0, 8, 32);
            }  else if (lod == 2) {
                return IsInside(pos, viewLod2, 16, 128) && !IsInside(pos, viewLod1, 12, 64);
            }

            return false;
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
            for (int y = -1; y <= size; y++)
            for (int z = -1; z <= size; z++)
            for (int x = -1; x <= size; x++) {

                var pos = viewLod + new IndexPos(x, y, z) * chunkSize;

                if (pos.x < 0 || pos.y < 0 || pos.z < 0) {
                    continue;
                }

                if (excludeViewLod.HasValue && IsInside(pos, excludeViewLod.Value, excludeSize, excludeChunkSize)) {
                    continue;
                }

                requestedPositions[lod].Add(pos);

                if (!AllChunksData[lod].TryGetValue(pos, out var chunkData)) {
                    Builder.RequestChunk(pos, lod);
                } else {
                    chunkData.RequestMesh();
                }
            }
        }

        private void UnloadNotRequiredChunks() {
            for (int lod = 0; lod < Region.TotalLods; lod++) {
                var activeDict = AllChunksData[lod];
                var currentValidSet = requestedPositions[lod];

                foreach (var entry in activeDict) {
                    var pos = entry.Key;
                    var chunkData = entry.Value;
                    if (!currentValidSet.Contains(pos)) {
                        toRemoveList.Add(pos);
                        Builder.ReleaseChunk(chunkData.Pos, chunkData.Lod);
                    }
                }

                foreach (var pos in toRemoveList) {
                    activeDict.Remove(pos);
                }
                toRemoveList.Clear();
            }
        }

        public void UnloadUnusedChunks() {
            for (int lod = 0; lod < Region.TotalLods; lod++) {
                var activeDict = AllChunksData[lod];

                foreach (var chunkData in activeDict.Values) {
                    if (chunkData.Chunk == null) {
                        Builder.ReleaseChunk(chunkData.Pos, chunkData.Lod);
                    }
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