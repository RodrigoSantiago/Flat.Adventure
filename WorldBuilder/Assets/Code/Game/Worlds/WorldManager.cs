using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Game.Data;
using Game.Data.Queues;
using Game.Entities.Player;
using Game.Utils;
using Game.Worlds.Generation;
using Game.Worlds.Rendering;
using K4os.Compression.LZ4;
using UnityEngine;
using Random = UnityEngine.Random;
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
        public int maxLodView = Region.MaxLod;

        private bool init;
        private IndexPos prevCenter;
        public IndexPos prevViewLod0;
        public IndexPos prevViewLod1;
        public IndexPos prevViewLod2;
        public IndexPos prevViewLod3;

        public string Storage { get; }
        public string Name { get; }
        public long Id { get; private set; }
        public WorldCache Cache { get; }
        public WorldBuilder Builder { get; }
        public WorldRender Render { get; }
        
        public WorldData Data { get; private set; }
        public WorldHeightMap Height { get; private set; }

        private Dictionary<IndexPos, ChunkRenderData>[] AllChunksData { get; }
        private Dictionary<IndexPos, int> AnyChunksData { get; } = new();
        private HashSet<IndexLodPos> delayedRequestSwipe;
        private readonly HashSet<IndexLodPos> delayedRequest1 = new();
        private readonly HashSet<IndexLodPos> delayedRequest2 = new();

        private readonly HashSet<IndexPos>[] requestedPositions;
        private readonly HashSet<IndexPos>[] renderedPositions;
        private readonly List<IndexPos> toRemoveList = new ();

        public Exception LoadingError { get; private set; }
        public bool IsLoading => loading != null;
        public float LoadingProgress => token?.Progress ?? 0;
        
        private bool cancelled;
        private float unloadTimer;
        
        public WorldManager(string name, string storage) {
            Name = name;
            Storage = storage;
            Id = 100;
            
            if (!Directory.Exists(storage)) {
                Directory.CreateDirectory(storage);
            }
            if (!Directory.Exists(storage + "/Cache")) {
                Directory.CreateDirectory(storage + "/Cache");
            }
            if (!Directory.Exists(storage + "/WorldMapCache")) {
                Directory.CreateDirectory(storage + "/WorldMapCache");
            }

            Cache = new WorldCache(this);
            Builder = new WorldBuilder(this);
            Builder.OnChunkLoaded = OnChunkLoaded;
            Builder.OnChunkCorrupted = (pos, lod, err) => {
                Debug.LogError(err);
            };
            
            lodConfig[0] = new LodConfig(viewLod0, 32, 8, 8, false);
            lodConfig[1] = new LodConfig(viewLod1, 64, 12, 12, false);
            lodConfig[2] = new LodConfig(viewLod2, 128, 14, 14, false);
            lodConfig[3] = new LodConfig(viewLod3, 256, 16, 7, true);
            SetViewPoint(new IndexPos(0, 0, 0));

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

        private IEnumerator loading;

        public void Load() {
            loading = Loading().GetEnumerator();
        }

        private class ByteReference {
            public byte[] data;
        }

        private ProgressToken token;

        private IEnumerable Loading() {
            token = new();
            if (File.Exists(Storage + "/World.data")) {
                var worldDataRef = new ByteReference();
                var it = ReadDataFile(Storage + "/World.data", worldDataRef);
                while (it.MoveNext()) {
                    if (cancelled || LoadingError != null) yield break;
                    yield return 0;
                }
                if (cancelled || LoadingError != null) yield break;
                
                Data = BinarySerializer.Read<WorldData>(worldDataRef.data);
            } else {
                Data = new WorldData();
                int high = Random.Range(int.MinValue, int.MaxValue);
                int low = Random.Range(int.MinValue, int.MaxValue);
                Data.seed = ((long)high << 32) | (uint)low;
                Data.width = 1024 * 16;
                Data.height = 1024 * 8;
                Data.depth = 1024;
                
                var it = WriteDataFile(Storage + "/World.data", BinarySerializer.Write(Data));
                while (it.MoveNext()) {
                    if (cancelled || LoadingError != null) yield break;
                    yield return 0;
                }
                if (cancelled || LoadingError != null) yield break;
                
            }
            token.Advance(0.1f);

            var worldHeightDataRef = new ByteReference();
            var genIt = GenerateMapData(token.Derive(0.9f), worldHeightDataRef);
            while (genIt.MoveNext()) {
                if (cancelled || LoadingError != null) yield break;
                yield return 0;
            }
            if (cancelled || LoadingError != null) yield break;
                
            Height = new WorldHeightMap(worldHeightDataRef.data);
            token.Advance(1f);
            
            // Setup Player
            foreach (var player in GameData.Instance.players) {
                // if (player.world == this)
                var cvPlayer = new GameObject("Player").AddComponent<CvPlayerUnit>().Setup(this);
                cvPlayer.transform.position = new Vector3(2048, 512, 2048);
            }
        }

        private IEnumerator ReadDataFile(string path, ByteReference data) {
            var source = new CancellationTokenSource();
            var token = source.Token;
            
            var readTask = Task.Run(() => File.ReadAllBytesAsync(path, token), token);
            
            while (!readTask.IsCompleted && !cancelled) {
                yield return null; 
            }
            if (cancelled) {
                source.Cancel();
                yield break;
            }

            if (readTask.IsFaulted) {
                LoadingError = readTask.Exception;
                yield break;
            }
            data.data = readTask.Result;
            if (data.data == null) {
                LoadingError = new Exception("Failed to read: " + path);
            }
        }

        private IEnumerator WriteDataFile(string path, byte[] data) {
            var source = new CancellationTokenSource();
            var token = source.Token;
            
            var readTask = Task.Run(() => File.WriteAllBytesAsync(path, data, token), token);
            
            while (!readTask.IsCompleted && !cancelled) {
                yield return null; 
            }
            if (cancelled) {
                source.Cancel();
                yield break;
            }

            if (readTask.IsFaulted) {
                LoadingError = readTask.Exception;
                yield break;
            }
        }

        private IEnumerator GenerateMapData(ProgressToken progress, ByteReference data) {
            WorldMapGenerator.Instance.Generate(this, progress,
                (height) => { data.data = height; },
                (e) => { LoadingError = e; });
            
            while (LoadingError == null && data.data == null && !cancelled) {
                yield return null; 
            }
            if (LoadingError != null) {
                yield break;
            }
            if (cancelled) {
                WorldMapGenerator.Instance.Cancel(this);
                yield break;
            }
            if (data.data == null) {
                LoadingError = new Exception("Failed to GenerateMapData");
            }
        }
        
        public void SaveAndExit() {
            cancelled = true;
            loading = null;
            Builder.Cancel();
            WorldGenerator.Instance.Cancel(this);
            WorldMapGenerator.Instance.Cancel(this);
            
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

        private GameObject deb;
        private Dictionary<IndexPos, GameObject> debViews = new Dictionary<IndexPos, GameObject>();

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

            if (GameManager.Instance.debugRegions && !debViews.ContainsKey(chunk.Pos.GetChunkIndex(Region.MaxLod))) {
                if (deb == null) {
                    deb = new GameObject();
                }
                var reg = chunk.Pos.GetChunkIndex(Region.MaxLod);
                var obj = new GameObject(Region.GenName(reg));
                var col = obj.AddComponent<BoxCollider>();
                col.center = new Vector3(128, 128, 128);
                col.size = new Vector3(256, 256, 256);
                obj.transform.SetParent(deb.transform, false);
                obj.transform.position = (Vector3)reg;
                debViews[reg] = obj;
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

        public bool HasAnyInRegion(IndexPos pos) {
            return AnyChunksData.ContainsKey(pos.GetChunkIndex(Region.MaxLod));
        }

        public void SetViewPoint(IndexPos point) {
            if (point.y < 64) {
                point.y = 64;
            }
            if (point.y >= 960) {
                point.y = 960 - 1;
            }
            centerPoint = point;
            
            int chunkSize = 32;

            var center = point.GetChunkIndex(0) / chunkSize;
            var viewCenter = viewLod0 / chunkSize + 2;
            if (center.x < viewCenter.x || center.x >= viewCenter.x + 4 ||
                center.y < viewCenter.y || center.y >= viewCenter.y + 4 ||
                center.z < viewCenter.z || center.z >= viewCenter.z + 4) {
                viewCenter = center.RoundSnapTo(2) - 2;
                if (viewCenter.y < 2) viewCenter.y = 2;
                if (viewCenter.y > 26) viewCenter.y = 26;
            }

            var lod0 = viewCenter - 2;

            var viewCenter1 = viewLod1 / chunkSize + 4;
            if (lod0.x < viewCenter1.x || lod0.x + 8 > viewCenter1.x + 16 ||
                lod0.y < viewCenter1.y || lod0.y + 8 > viewCenter1.y + 16 ||
                lod0.z < viewCenter1.z || lod0.z + 8 > viewCenter1.z + 16) {
                viewCenter1 = center.RoundSnapTo(8) - 8;
                if (viewCenter1.y < 0) viewCenter1.y = 0;
                if (viewCenter1.y > 16) viewCenter1.y = 16;
            }

            var lod1 = viewCenter1 - 4;

            var viewCenter2 = viewLod2 / chunkSize + 4;
            viewCenter2.y = -4;
            if (lod1.x < viewCenter2.x || lod1.x + 24 > viewCenter2.x + 48 ||
                lod1.y < viewCenter2.y || lod1.y + 24 > viewCenter2.y + 48) {
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
            lodConfig[1] = new LodConfig(viewLod1, 64, 12, 12, maxLodView <= 1);
            lodConfig[2] = new LodConfig(viewLod2, 128, 14, 12, maxLodView <= 2);
            lodConfig[3] = new LodConfig(viewLod3, 256, 16, 6, true);
        }

        public void AddTeleportPoint(int pointId, IndexPos point) {
        }

        public void RemoveTeleportPoint(int pointId) {
        }

        public void RequestChunk(int lod, IndexPos chunkIndex) {
            delayedRequestSwipe.Add(new IndexLodPos(chunkIndex, lod));
        }

        public void Update() {
            if (loading != null) {
                if (loading.MoveNext()) {
                    return;
                }
                loading = null;
            }
            RequestChunks();

            if (delayedRequestSwipe == delayedRequest1) {
                delayedRequestSwipe = delayedRequest2;
                
                foreach (var request in delayedRequest1) {
                    Builder.RequestChunk(request.pos, request.lod);
                }
                delayedRequest1.Clear();
            } else {
                delayedRequestSwipe = delayedRequest1;
                
                foreach (var request in delayedRequest2) {
                    Builder.RequestChunk(request.pos, request.lod);
                }
                delayedRequest2.Clear();
            }

            if (Time.time - unloadTimer > 3.0f) {
                Render.CollectFrame(renderedPositions);
                
                UnloadNotRequiredChunks();
                
                foreach (var renderedPosition in renderedPositions) {
                    renderedPosition.Clear();
                }

                for (int lod = 0; lod < Region.TotalLods; lod++) {
                    var activeDict = AllChunksData[lod];

                    foreach (var chunkData in activeDict.Values) {
                        chunkData.RequestMesh();
                        chunkData.ReleaseData();
                    }
                }
            
                ReleaseUnusedChunkData();
                
                unloadTimer = Time.time;
            }
            
            Render.RenderFrame();
        }

        public void RequestChunks() {
            if (!init) {
                init = true;
            } else {
                if (prevViewLod0 == viewLod0
                    && prevViewLod1 == viewLod1 
                    && prevViewLod2 == viewLod2 
                    && prevViewLod3 == viewLod3
                    && maxLodView == GameManager.Instance.maxLodView) {
                    return;
                }
            }

            if (prevCenter != centerPoint) {
                Cache.SetPriorityCenter(centerPoint);
                GameManager.Instance.WorldGenerator.SetPriorityCenter(centerPoint);
                GameManager.Instance.MeshGenerator.SetPriorityCenter(centerPoint);
            }
            
            prevCenter = centerPoint;
            prevViewLod0 = viewLod0;
            prevViewLod1 = viewLod1;
            prevViewLod2 = viewLod2;
            prevViewLod3 = viewLod3;
            maxLodView = GameManager.Instance.maxLodView;

            foreach (var requestedPosition in requestedPositions) {
                requestedPosition.Clear();
            }
            
            RequestLod(0, lodConfig[0], null);
            if (maxLodView >= 1) RequestLod(1, lodConfig[1], lodConfig[0].Padding());
            if (maxLodView >= 2) RequestLod(2, lodConfig[2], lodConfig[1].Padding());
            if (maxLodView >= 3) RequestLod(3, lodConfig[3], lodConfig[2].Padding());
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

            Cache.ReleaseUnusedChunkCache();
        }
    }
}