using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Data;
using Game.DebugUtils;
using Game.Worlds.Generation;
using Game.Worlds.Rendering;
using UnityEngine;
using WorldCache = Game.Worlds.CacheManagement.WorldCache;

namespace Game.Worlds {
    public class WorldManager {

        public IndexPos centerPoint;
        public IndexPos view;
        public IndexPos viewLod0;
        public IndexPos viewLod1;
        public IndexPos viewLod2;
        public IndexPos viewLod3;
        public LodConfig[] lodConfig = new LodConfig[4];

        private bool init;
        private IndexPos prevCenter;
        public IndexPos prevViewLod0;
        public IndexPos prevViewLod1;
        public IndexPos prevViewLod2;
        public IndexPos prevViewLod3;

        public string Storage { get; }
        public WorldCache Cache { get; }
        public WorldGenerator Generator { get; }
        public WorldBuilder Builder { get; }
        public WorldRender Render { get; }

        private Dictionary<IndexPos, ChunkRenderData>[] AllChunksData { get; }
        private readonly Dictionary<IndexPos, int> AnyChunksData = new();
        private readonly HashSet<IndexLodPos> delayedRequest = new();
        private readonly HashSet<IndexLodPos> delayedRequestTemp = new();

        private readonly HashSet<IndexPos>[] requestedPositions;
        private readonly HashSet<IndexPos>[] renderedPositions;
        private readonly List<IndexPos> toRemoveList = new ();

        private float unloadTimer;
        
        public WorldManager(string storage) {
            Storage = storage;
            
            if (!Directory.Exists(storage)) {
                Directory.CreateDirectory(storage);
            }

            Cache = new WorldCache(this);
            Generator = new WorldGenerator(this);
            Builder = new WorldBuilder(this);
            Builder.OnChunkLoaded = OnChunkLoaded;
            Builder.OnChunkCorrupted = (pos, lod, err) => {
                Debug.LogError(err);
            };
            
            lodConfig[0] = new LodConfig(viewLod0, 32, 8, 8, false);
            lodConfig[1] = new LodConfig(viewLod1, 64, 12, 12, false);
            lodConfig[2] = new LodConfig(viewLod2, 128, 14, 14, false);
            lodConfig[3] = new LodConfig(viewLod3, 256, 16, 7, true);

            AllChunksData = new Dictionary<IndexPos, ChunkRenderData>[Region.TotalLods];
            requestedPositions = new HashSet<IndexPos>[Region.TotalLods];
            renderedPositions = new HashSet<IndexPos>[Region.TotalLods];

            for (int i = 0; i < Region.TotalLods; i++) {
                AllChunksData[i] = new Dictionary<IndexPos, ChunkRenderData>();
                requestedPositions[i] = new HashSet<IndexPos>();
                renderedPositions[i] = new HashSet<IndexPos>();
            }
            
            Render = new WorldRender(this);
        }

        public void SaveAndExit() {
            Generator.Dispose();
            Builder.Cancel();
            
            for (int lod = 0; lod < Region.TotalLods; lod++) {
                var activeDict = AllChunksData[lod];

                foreach (var chunkData in activeDict.Values) {
                    if (!chunkData.ChunkReleased) {
                        chunkData.ChunkReleased = true;
                        Builder.ReleaseChunk(chunkData.Pos, chunkData.Lod);
                    }
                    chunkData.Dispose();
                }
                activeDict.Clear();
            }

            Cache.ConsumeAndDispose();
            Render.Dispose();
        }

        public void OnChunkLoaded(Chunk chunk) {
            if (!AllChunksData[chunk.Lod].TryGetValue(chunk.Pos, out var chunkData)) {
                chunkData = new ChunkRenderData(this, chunk);
                AllChunksData[chunk.Lod][chunk.Pos] = chunkData;
                
                var reg = chunk.Pos.GetChunkIndex(Region.MaxLod);
                AnyChunksData.TryAdd(reg, 0);
                AnyChunksData[reg]++;
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

        public bool HasAnyChunkData(IndexPos pos) {
            return AnyChunksData.ContainsKey(pos.GetChunkIndex(Region.MaxLod));
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

            var viewCenter2 = viewLod2 / chunkSize + 4;
            if (lod1.x < viewCenter2.x || lod1.x + 24 > viewCenter2.x + 48 ||
                lod1.y < viewCenter2.y || lod1.y + 24 > viewCenter2.y + 48 ||
                lod1.z < viewCenter2.z || lod1.z + 24 > viewCenter2.z + 48) {
                viewCenter2 = center.RoundSnapTo(24) - 28;
            }

            var lod2 = viewCenter2 - 4;

            view = viewCenter * chunkSize;
            viewLod0 = lod0 * chunkSize;
            viewLod1 = lod1 * chunkSize;
            viewLod2 = lod2 * chunkSize;
            viewLod3 = viewLod1 - (52 * chunkSize);
            viewLod3.y = viewLod2.y - (chunkSize * 4);
            viewLod3 = viewLod3.RoundSnapTo(256);

            lodConfig[0] = new LodConfig(viewLod0, 32, 8, 8, false);
            lodConfig[1] = new LodConfig(viewLod1, 64, 12, 12, false);
            lodConfig[2] = new LodConfig(viewLod2, 128, 14, 14, false);
            lodConfig[3] = new LodConfig(viewLod3, 256, 16, 7, true);
        }

        public void AddTeleportPoint(int pointId, IndexPos point) {
        }

        public void RemoveTeleportPoint(int pointId) {
        }

        public void RequestChunk(int lod, IndexPos chunkIndex) {
            delayedRequestTemp.Add(new IndexLodPos(chunkIndex, lod));
        }

        public void Update() {
            var requestChunks = DateTime.Now;
            RequestChunks();
            
            var delayedQuest = DateTime.Now;
            delayedRequest.UnionWith(delayedRequestTemp);
            delayedRequestTemp.Clear();
            foreach (var request in delayedRequest) {
                Builder.RequestChunk(request.pos, request.lod);
            }
            delayedRequest.Clear();
            
            var renderFrame = DateTime.Now;
            Render.RenderFrame();

            if (Time.time - unloadTimer > 3.0f) {
                Render.CollectFrame(renderedPositions);
                
                UnloadNotRequiredChunks();
                
                foreach (var renderedPosition in renderedPositions) {
                    renderedPosition.Clear();
                }

                for (int lod = 0; lod < Region.TotalLods; lod++) {
                    var activeDict = AllChunksData[lod];

                    foreach (var chunkData in activeDict.Values) {
                        chunkData.ReleaseData();
                    }
                }
                
                unloadTimer = Time.time;
            }
            
            var releaseUnused = DateTime.Now;
            ReleaseUnusedChunkData();
            GameDebug.AddMetric("Request", delayedQuest - requestChunks);
            GameDebug.AddMetric("Delayed", renderFrame - delayedQuest);
            GameDebug.AddMetric("Render", releaseUnused - renderFrame);
            GameDebug.AddMetric("Release", DateTime.Now - releaseUnused);
        }

        public void RequestChunks() {
            if (!init) {
                init = true;
            } else {
                if (prevViewLod0 == viewLod0
                    && prevViewLod1 == viewLod1 
                    && prevViewLod2 == viewLod2 
                    && prevViewLod3 == viewLod3) {
                    return;
                }
            }

            if (prevCenter != centerPoint) {
                Cache.SetPriorityCenter(centerPoint);
                Generator.SetPriorityCenter(centerPoint);
                GameManager.Instance.MeshGenerator.SetPriorityCenter(centerPoint);
            }
            
            prevCenter = centerPoint;
            prevViewLod0 = viewLod0;
            prevViewLod1 = viewLod1;
            prevViewLod2 = viewLod2;
            prevViewLod3 = viewLod3;

            foreach (var requestedPosition in requestedPositions) {
                requestedPosition.Clear();
            }

            RequestLod(0, lodConfig[0], null);
            RequestLod(1, lodConfig[1], lodConfig[0].Padding());
            RequestLod(2, lodConfig[2], lodConfig[1].Padding());
            RequestLod(3, lodConfig[3], lodConfig[2].Padding());
            
            unloadTimer = Time.time;
        }

        public bool IsChunkRenderRequired(int lod, IndexPos pos) {
            if (lod == 0) {
                return lodConfig[0].IsInside(pos);
            } else if (lod == 1) {
                return lodConfig[1].IsInside(pos) && !lodConfig[0].IsInside(pos);
            } else if (lod == 2) {
                return lodConfig[2].IsInside(pos) && !lodConfig[1].IsInside(pos);
            } else if (lod == 3) {
                return lodConfig[3].IsInside(pos) && !lodConfig[2].IsInside(pos);
            }

            return false;
        }

        private void RequestLod(
            int lod,
            LodConfig config,
            LodConfig? excludeViewLod
        ) {
            if (!GameManager.Instance.controlRender[lod]) return;
            
            for (int y = -1; y <= config.Height; y++)
            for (int z = -1; z <= config.Size; z++)
            for (int x = -1; x <= config.Size; x++) {

                var pos = config.ViewLod + new IndexPos(x, y, z) * config.ChunkSize;

                if (pos.x < 0 || pos.y < 0 || pos.z < 0 || pos.y >= Region.MaxHeight) {
                    continue;
                }

                if (excludeViewLod?.IsInside(pos) == true) {
                    continue;
                }

                if (config.Cylinder) {
                    bool inside = false;
                    for (int tz = -1; tz < 1 && !inside; tz++) 
                    for (int tx = -1; tx < 1; tx++) {
                        if (config.IsInside(pos + new IndexPos(tx, 0, tz) * config.ChunkSize)) {
                            inside = true;
                            break;
                        }
                    }
                    if (!inside) continue;
                }

                requestedPositions[lod].Add(pos);

                if (!AllChunksData[lod].TryGetValue(pos, out var chunkData) || chunkData.IsReleased) {
                    Builder.RequestChunk(pos, lod);
                } else {
                    chunkData.RequestMesh();
                }
            }
        }

        private void UnloadNotRequiredChunks() {
            for (int lod = 0; lod < Region.TotalLods; lod++) {
                var activeDict = AllChunksData[lod];
                var requested = requestedPositions[lod];
                var rendered = renderedPositions[lod];

                foreach (var chunkData in activeDict.Values) {
                    var pos = chunkData.Pos;
                    if (!requested.Contains(pos) && !rendered.Contains(pos)) {
                        chunkData.Dispose();
                        toRemoveList.Add(pos);
                        if (!chunkData.ChunkReleased) {
                            Builder.ReleaseChunk(chunkData.Pos, chunkData.Lod);
                        }
                    }
                }

                foreach (var pos in toRemoveList) {
                    activeDict.Remove(pos);
                    var key = pos.GetChunkIndex(Region.MaxLod);
                    if (--AnyChunksData[key] <= 0) {
                        AnyChunksData.Remove(key);
                    }
                }
                toRemoveList.Clear();
            }
        }

        public void ReleaseUnusedChunkData() {
            for (int lod = 0; lod < Region.TotalLods; lod++) {
                var activeDict = AllChunksData[lod];

                foreach (var chunkData in activeDict.Values) {
                    if (chunkData.Chunk == null && !chunkData.ChunkReleased) {
                        chunkData.ChunkReleased = true;
                        Builder.ReleaseChunk(chunkData.Pos, chunkData.Lod);
                    }
                }
            }
        }
    }
}