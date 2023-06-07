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
        public static int maxViewLod = 4;
        public static ChunkManager current;
        public static ChunkPhysics physics { get => current.chunkPhysics; }
        
        public Material groundMaterial;
        public ComputeShader marchingCubesCompute;
        public GameObject objPreview;
        public GameObject objPreview2;
        public GameObject objCube;
        public Material[] matPreview;
        public bool renderSplit = false;
        public bool renderTrans = true;

        public WorldPlayerController controller;
        public WorldSettings settings;
        
        // Settings
        public int minViewSize = 8;
        public int maxViewSize = 8;
        public int viewHeight = 16;
        public int[] lodCount;
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

        public GameObject[] instances;
        public Transform rayCastIn;
        public Transform rayCastOut;
        public LineRenderer line;
        public LineRenderer line2;

        // Composition
        private ChunkMeshGenerator meshGenerator;
        private ChunkPhysics chunkPhysics;
        private ChunkRenderer chunkRenderer;

        // Cache Variables
        private Dictionary<Vector3Int, ChunkHolder>[] chunks = new Dictionary<Vector3Int, ChunkHolder>[maxViewLod];
        private Vector3Int[] lodCenter = new Vector3Int[maxViewLod];
        private ChunkLodBox[] lodBox = new ChunkLodBox[maxViewLod];
        private ChunkLodBox[] lodBoxIn = new ChunkLodBox[maxViewLod];
        
        private int ready = -1;
        private Vector3Int renderLocal;
        private Vector3Int requestLocal;
        private Vector3Int prevRequestLocal;
        private Vector3Int prevRequestLocalLod1;
        
        private void Awake() {
            current = this;
            lodCount = new int[maxViewLod];
            //instances = new GameObject[maxViewLod];
            for (int i = 0; i < maxViewLod; i++) {
                chunks[i] = new Dictionary<Vector3Int, ChunkHolder>();
                lodBox[i] = new ChunkLodBox(i);
                lodBoxIn[i] = new ChunkLodBox(i);
                //instances[i] = Instantiate(objCube, Vector3.zero, Quaternion.identity);
            }
        }

        private void OnEnable() {
            if (meshGenerator == null) {
                //meshGenerator = new ChunkMeshGeneratorCPU(OnChunkRemesh);
                meshGenerator = new ChunkMeshGeneratorGPU(marchingCubesCompute, OnChunkRemesh, OnChunkRemeshLod);
                meshGenerator.Init();
            }

            chunkPhysics ??= new ChunkPhysics(this, chunks[0]);
            chunkRenderer ??= new ChunkRenderer(settings, chunks, groundMaterial, maxViewLod);
            
            renderLocal = local;
            RefreshLod();
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

            if (chunkPhysics != null) {
                Vector3 from = rayCastIn.position;
                Vector3 to = rayCastOut.position;
                if (chunkPhysics.Raycast(from, Vector3.Normalize(to - from), Vector3.Distance(from, to),
                        out var dist, out var collision, out var normal, out var voxel)) {
                }
                line2.SetPosition(0, collision);
                line2.SetPosition(1, collision + (normal * 2));
                line2.SetPosition(2, collision);
                line2.SetPosition(3, collision + Vector3.Cross(normal, new Vector3(1, 0, 0)) * 2);

                line.SetPosition(0, from);
                line.SetPosition(1, collision);
            }
        }

        private void LateUpdate() {
            RemeshChunks();
            chunkRenderer.renderSplit = renderSplit;
            chunkRenderer.Render();
        }

        public void OnChunkReceived(Chunk chunk) {
            lodCount[chunk.lod]++;
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

        public void OnChunkRemeshLod(Chunk chunk, Mesh meshLod, Vector3Int minLimit, Vector3Int maxLimit) {
            chunks[chunk.lod][chunk.local].MeshLodReady(meshLod, minLimit, maxLimit);
            RemeshChunks();
        }

        private void RequestLoop() {
            if (prevRequestLocal != lodCenter[0]) {
                prevRequestLocal = lodCenter[0];
                ready = -1;
                RequestToPlay(0);
            }

            for (int lod = 0; lod < maxViewLod; lod++) {
                if (ready == lod - 1) {
                    if (IsLodReady(lod)) {
                        ready = lod;
                        if (lod + 1 < maxViewLod)
                            RequestToPlay(lod + 1);
                    }
                }
            }

            if (ready >= 0) {
                renderLocal = prevRequestLocal;
            }
        }

        private void RequestToPlay(int lod) {
            List<Vector3Int> requests = new List<Vector3Int>();
            int n = 16 * (1 << lod);
            for (int y = Math.Max(0, lodBox[lod].min.y - n); y < Math.Min(settings.height, lodBox[lod].max.y + n); y += n)
            for (int x = lodBox[lod].min.x - n; x < lodBox[lod].max.x + n; x += n)
            for (int z = lodBox[lod].min.z - n; z < lodBox[lod].max.z + n; z += n) {
                var key = settings.Pos(new Vector3Int(x, y, z));
                if (lod > 0 && lodBoxIn[lod].Contains(key)) continue;
                if (settings.IsInside(key) && !chunks[lod].ContainsKey(key)) {
                    requests.Add(key);
                }
            }
            
            SortRequests(requests, lodCenter[lod]);
            foreach (var request in requests) {
                chunks[lod][request] = new ChunkHolder(lod, request);
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
            
            for (int i = 0; i < maxViewLod; i++) {
                int n1 = 16 * (1 << i);
                int n2 = i + 1 == maxViewLod ? 16 * (1 << i) : 16 * (1 << (i + 1));
                lodCenter[i] = local / n2 * n2;
                lodBox[i].min = lodCenter[i] - view * n1;
                lodBox[i].size = size * n1;
                if (i > 0) {
                    lodBoxIn[i].min = lodBox[i - 1].min + new Vector3Int(n1, n1, n1);
                    lodBoxIn[i].size = lodBox[i - 1].size - new Vector3Int(n1, n1, n1) * 2;
                }

                // var p = lodBox[i].min;
                // p.y = Mathf.Max(0, p.y);
                // var s = lodBox[i].size;
                // s.y = Mathf.Min(settings.height, p.y + s.y) - p.y;
                //instances[i].transform.position = p;
                //instances[i].transform.localScale = s;
                
                chunkRenderer.UpdateRenderBox(i, lodBox[i]);
            }
        }
        
        public bool IsLodReady(int lod) {
            int n = 16 * (1 << lod);
            for (int y = Math.Max(0, lodBox[lod].min.y); y < Math.Min(settings.height, lodBox[lod].max.y); y += n)
            for (int x = lodBox[lod].min.x; x < lodBox[lod].max.x; x += n)
            for (int z = lodBox[lod].min.z; z < lodBox[lod].max.z; z += n) {
                var key = settings.Pos(new Vector3Int(x, y, z));
                if (lod > 0 && lodBox[lod - 1].Contains(new Vector3Int(x, y, z))) continue;
                if (settings.IsInside(key) && (!chunks[lod].TryGetValue(key, out var holder) || !holder.ready)) {
                    return false;
                }
            }

            return true;
        }

        private void RemeshChunks() {
            if (meshGenerator == null || meshGenerator.isRemeshing) return;

            for (int lod = 0; lod < maxViewLod; lod++) {
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
                    } else if (holder.mesh != null && lod + 1 < maxViewLod) {
                        if (lodBox[lod].LodLimit(loc, out var minlimit, out var maxLimit) && !holder.IsLodReady(minlimit, maxLimit)) {
                            if (!meshGenerator.RemeshChunkLod(holder.chunk, settings, chunks[lod], minlimit, maxLimit)) {
                                holder.MeshLodReady(null, minlimit, maxLimit);
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
        
        private void DebugPreview(Vector3Int pos) {
            var holder = chunks[0][pos];
            for (int x = 0; x < 16; x++) 
            for (int y = 0; y < 16; y++) 
            for (int z = 0; z < 16; z++) {
                var voxel = holder.chunk[x, y, z];
                var obj = Instantiate(voxel.volume > 0.5 ? objPreview : objPreview2);
                obj.transform.position = new Vector3(x, y, z) + pos;
                obj.transform.localScale = Vector3.Lerp(new Vector3(0.05f, 0.05f, 0.05f), new Vector3(0.25f, 0.25f, 0.25f), voxel.volume);
                obj.GetComponent<Renderer>().material = matPreview[voxel.material];
            }
        }
    }
}