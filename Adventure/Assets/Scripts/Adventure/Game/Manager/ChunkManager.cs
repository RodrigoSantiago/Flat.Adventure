using System;
using System.Collections.Generic;
using Adventure.Game.Manager.ChunkManagment;
using Adventure.Game.Manager.ShapeGeneration;
using Adventure.Logic;
using Adventure.Logic.Data;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Adventure.Game.Manager {
    
    public class ChunkManager : MonoBehaviour {

        public Material groundMaterial;
        public ComputeShader marchingCubesCompute;
        public GameObject objPreview;
        public GameObject objPreview2;
        public Material[] matPreview;
        public bool renderSplit = false;

        private ChunkMeshGenerator meshGenerator;

        public WorldPlayerController controller;
        public WorldSettings settings;
        
        public int minViewSize = 8;
        public int maxViewSize = 8;
        public int viewHeight = 16;
        public Vector3 position = new Vector3(64, 0, 64);
        public Vector3Int local {
            get {
                return new Vector3Int(
                    Mathf.FloorToInt(position.x / 16) * 16,
                    Mathf.FloorToInt(position.y / 16) * 16,
                    Mathf.FloorToInt(position.z / 16) * 16
                );
            }
        }

        private Dictionary<Vector3Int, ChunkHolder>[] chunks;
        private Vector3Int[] lodCenter = new Vector3Int[2];
        private ChunkLodBox[] lodBox = new ChunkLodBox[2];
        private ChunkLodBox[] lodBoxIn = new ChunkLodBox[2];
        private ChunkLodBox[] renderLodBox = new ChunkLodBox[2];
        private ChunkLodBox[] renderLodBoxIn = new ChunkLodBox[2];

        private bool ready;
        private Vector3Int renderLocal;
        private Vector3Int requestLocal;
        private Vector3Int prevRequestLocal;
        private Vector3Int prevRequestLocalLod1;
        
        private void Start() {
            chunks = new Dictionary<Vector3Int, ChunkHolder>[2];
            for (int i = 0; i < chunks.Length; i++) {
                chunks[i] = new Dictionary<Vector3Int, ChunkHolder>();
            }

            RefreshLod();
            renderLocal = local;
            for (int i = 0; i < renderLodBox.Length; i++) {
                renderLodBox[i] = lodBox[i];
                renderLodBoxIn[i] = lodBoxIn[i];
            }
            
            UnsafeUtility.SetLeakDetectionMode(NativeLeakDetectionMode.Enabled);
        }

        private void OnEnable() {
            if (meshGenerator == null) {
                //meshGenerator = new ChunkMeshGeneratorCPU(OnChunkRemesh);
                meshGenerator = new ChunkMeshGeneratorGPU(marchingCubesCompute, OnChunkRemesh, OnChunkRemeshLod);
                meshGenerator.Init();
            }
        }

        private void OnDisable() {
            meshGenerator.Release();
            meshGenerator = null;
        }

        private void Update() {
            RefreshLod();
            RequestLoop();

            if (Input.GetKeyDown(KeyCode.Space)) {
                position.x += 8;
            }
            if (Input.GetKeyDown(KeyCode.A)) {
                
            }
        }

        private void LateUpdate() {
            RemeshChunks();
            RenderChunks();
        }

        public void OnChunkReceived(Chunk chunk) {
            if (chunks[chunk.lod].TryGetValue(chunk.local, out var holder)) {
                holder.ChunkReady(chunk);
            }
        }

        public void OnChunkRemesh(Chunk chunk, Mesh mesh) {
            chunks[chunk.lod][chunk.local].MeshReady(mesh);
            RemeshChunks();
            
            /*if (mesh != null) {
                var obj = Instantiate(meshPreview.gameObject);
                obj.transform.position = chunk.local;
                obj.GetComponent<MeshFilter>().mesh = mesh;
                //ChunkMeshGenerator.MeshToCpu(mesh);
            }*/
        }

        public void OnChunkRemeshLod(Chunk chunk, Mesh meshLod) {
            chunks[chunk.lod][chunk.local].MeshLodReady(meshLod);
            RemeshChunks();
        }

        private void RequestLoop() {
            if (prevRequestLocal != lodCenter[0]) {
                prevRequestLocal = lodCenter[0];
                ready = false;
                RequestToPlay(0);
            }
            
            if (!ready) {
                ready = IsLodReady(0);
                if (ready) {
                    RequestToPlay(1);
                }
            }

            if (ready) {
                renderLocal = prevRequestLocal;
                for (int i = 0; i < renderLodBox.Length; i++) {
                    renderLodBox[i] = lodBox[i];
                    renderLodBoxIn[i] = lodBoxIn[i];
                }
            }
        }

        private void RequestToPlay(int lod) {
            List<Vector3Int> requests = new List<Vector3Int>();
            int n = 16 * (1 << lod);
            for (int x = lodBox[lod].min.x - n; x < lodBox[lod].max.x + n; x += n)
            for (int y = lodBox[lod].min.y - n; y < lodBox[lod].max.y + n; y += n)
            for (int z = lodBox[lod].min.z - n; z < lodBox[lod].max.z + n; z += n) {
                var key = settings.Pos(new Vector3Int(x, y, z));
                if (lod > 0 && lodBoxIn[lod].Contains(key)) continue;
                if (settings.IsInside(key) && !chunks[lod].ContainsKey(key)) requests.Add(key);
            }

            SortRequests(requests, lodCenter[lod]);
            foreach (var request in requests) {
                chunks[lod][request] = new ChunkHolder(lod);
                controller.RequestChunk(request, lod);
            }
        }

        private void SortRequests(List<Vector3Int> requests, Vector3Int loc) {
            requests.Sort((a, b) => {
                a = settings.CloserPos(a, loc);
                b = settings.CloserPos(b, loc);

                int dista = (a.x - loc.x) * (a.x - loc.x) + (a.z - loc.z) * (a.z - loc.z);
                int distb = (b.x - loc.x) * (b.x - loc.x) + (b.z - loc.z) * (b.z - loc.z);
                return dista.CompareTo(distb);
            });
        }

        private void RefreshLod() {
            var view = new Vector3Int(minViewSize, minViewSize, minViewSize);
            var size = view * 2;
            
            for (int i = 0; i < 2; i++) {
                int n1 = 16 * (1 << i);
                int n2 = 16 * (1 << (i + 1));
                lodCenter[i] = local / n2 * n2;
                lodBox[i].min = lodCenter[i] - view * n1;
                lodBox[i].size = size * n1;
                if (i > 0) {
                    lodBoxIn[i].min = lodBox[i - 1].min + new Vector3Int(n1, n1, n1);
                    lodBoxIn[i].size = lodBox[i - 1].size - new Vector3Int(n1, n1, n1) * 2;
                }
            }
        }
        
        public bool IsLodReady(int lod) {
            int n = 16 * (1 << lod);
            for (int x = lodBox[lod].min.x; x < lodBox[lod].max.x; x += n)
            for (int y = lodBox[lod].min.y; y < lodBox[lod].max.y; y += n)
            for (int z = lodBox[lod].min.z; z < lodBox[lod].max.z; z += n) {
                var key = settings.Pos(new Vector3Int(x, y, z));
                if (settings.IsInside(key) && (!chunks[lod].TryGetValue(key, out var holder) || !holder.ready)) {
                    return false;
                }
            }

            return true;
        }

        private void RemeshChunks() {
            if (meshGenerator.isRemeshing) return;

            for (int lod = 0; lod < 2; lod++) {
                foreach (var (loc, holder) in chunks[lod]) {
                    if (holder.chunk == null) continue;
                    if (!holder.ready) {
                        if (CheckChunkNeighboors(holder)) {
                            holder.hasNeighboors = true;

                            if (!meshGenerator.RemeshChunk(holder.chunk, settings, chunks[lod])) {
                                holder.MeshReady(null);
                            } else {
                                return;
                            }
                        }
                    } else if (holder.mesh != null && !holder.lodReady) {
                        if (lodBox[lod].LodLimit(loc, out var minlimit, out var maxLimit)) {
                            if (!meshGenerator.RemeshChunkLod(holder.chunk, settings, chunks[lod], minlimit, maxLimit)) {
                                holder.MeshLodReady(null);
                            } else {
                                return;
                            }
                        }

                        if (renderLodBox[lod].LodLimit(loc, out  minlimit, out maxLimit)) {
                            if (!meshGenerator.RemeshChunkLod(holder.chunk, settings, chunks[lod], minlimit, maxLimit)) {
                                holder.MeshLodReady(null);
                            } else {
                                return;
                            }
                        }
                    }
                }
            }
        }

        private bool CheckChunkNeighboors(ChunkHolder holder) {
            var lod = holder.lod;
            var loc = holder.chunk.local;
            int n = (16 * (1 << lod));
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++) {
                var key = settings.Pos(loc + new Vector3Int(x, y, z) * n);
                if (!settings.IsInside(key)) continue;

                if (!chunks[lod].TryGetValue(key, out var chunkHolder) || chunkHolder.chunk == null) {
                    return false;
                }
            }

            return true;
        }

        private void RenderChunks() {
            RenderParams renderParams = new RenderParams(groundMaterial);

            float off = 0.01f;
            foreach (var entry in chunks[0]) {
                var m = entry.Value.mesh;
                var lm = entry.Value.lodMesh;
                if (m == null) continue;
                
                var pos = settings.CloserPos(entry.Key, renderLocal);
                
                if (renderLodBox[0].Contains(pos)) {
                    Vector3 extraPos = Vector3.zero;
                    Vector3 scale = Vector3.one;
                    
                    if (renderLodBox[0].LodLimit(pos, out var minLimit, out var maxLimit)) {
                        /*extraPos = new Vector3(minLimit.x * off, minLimit.y * off, minLimit.z * off);
                        scale = new Vector3(
                            (16f - (minLimit.x + maxLimit.x) * off) / 16f, 
                            (16f - (minLimit.y + maxLimit.y) * off) / 16f, 
                            (16f - (minLimit.z + maxLimit.z) * off) / 16f);*/
                        if (lm != null) {
                            var ma = Matrix4x4.Translate(pos) * Matrix4x4.Scale(Vector3.one * (renderSplit ? 0.95f : 1));
                            Graphics.RenderMesh(in renderParams, lm, 0, ma);
                        }
                    }
                    var matrix = Matrix4x4.Translate(pos + extraPos) * Matrix4x4.Scale(scale * (renderSplit ? 0.95f : 1));
                    Graphics.RenderMesh(in renderParams, m, 0, matrix);
                }
            }

            foreach (var entry in chunks[1]) {
                var m = entry.Value.mesh;
                if (m == null) continue;

                var pos = settings.CloserPos(entry.Key, renderLocal);
                if (renderLodBox[1].Contains(pos) && !renderLodBox[0].Contains(pos)) {
                    var matrix = Matrix4x4.Translate(pos) * Matrix4x4.Scale(Vector3.one * (renderSplit ? 1.90f : 2));
                    Graphics.RenderMesh(in renderParams, m, 0, matrix);
                }
            }
        }

        private void DebugPreview() {
            var holder = chunks[0][Vector3Int.zero];
            for (int x = 0; x < 16; x++) 
            for (int y = 0; y < 16; y++) 
            for (int z = 0; z < 16; z++) {
                var voxel = holder.chunk[x, y, z];
                var obj = Instantiate(voxel.volume > 0.5 ? objPreview : objPreview2);
                obj.transform.position = new Vector3(x, y, z);
                obj.transform.localScale = Vector3.Lerp(new Vector3(0.05f, 0.05f, 0.05f), new Vector3(0.25f, 0.25f, 0.25f), voxel.volume);
                obj.GetComponent<Renderer>().material = matPreview[voxel.material];
            }
        }
    }
}