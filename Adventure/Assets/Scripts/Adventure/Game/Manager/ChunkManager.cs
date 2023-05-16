using System;
using System.Collections.Generic;
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

        private List<Vector3Int> chunkRequest = new();
        private Dictionary<Vector3Int, ChunkHolder> chunks = new();
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

        private bool ready;
        private Vector3Int prevLocal;
        private Vector3Int prevReadyLocal;
        
        private void Start() {
            UnsafeUtility.SetLeakDetectionMode(NativeLeakDetectionMode.Enabled);
        }

        public bool IsReady() {
            if (prevLocal != local) {
                prevLocal = local;
                ready = IsReadyToPlay();
            }
            
            if (!ready) {
                ready = IsReadyToPlay();
                if (!ready && prevReadyLocal != local) {
                    prevReadyLocal = local;
                    RequestToPlay();
                }
            }

            return ready;
        }
        
        private bool IsReadyToPlay() {
            Vector3Int loc = local;
            for (int x = -minViewSize-1; x <= minViewSize; x++) {
                for (int z = -minViewSize-1; z <= minViewSize; z++) {
                    for (int y = 0; y < viewHeight; y++) {
                        var key = settings.Pos(new Vector3Int(loc.x + (x * 16), y * 16, loc.z + (z * 16)));
                        if (!settings.IsInside(key)) continue;
                        if (!chunks.ContainsKey(key)) return false;
                    }
                }
            }

            return true;
        }

        private void RequestToPlay() {
            Vector3Int loc = local;
            List<Vector3Int> requests = new List<Vector3Int>();
            for (int x = -minViewSize-1; x <= minViewSize; x++) {
                for (int z = -minViewSize-1; z <= minViewSize; z++) {
                    for (int y = 0; y < viewHeight; y++) {
                        var key = settings.Pos(new Vector3Int(loc.x + (x * 16), y * 16, loc.z + (z * 16)));
                        if (!settings.IsInside(key)) continue;
                        if (!chunks.ContainsKey(key) && !chunkRequest.Contains(key)) {
                            chunkRequest.Add(key);
                            requests.Add(key);
                        }
                    }
                }
            }

            requests.Sort((a, b) => {
                a = settings.CloserPos(a, local);
                b = settings.CloserPos(b, local);

                int dista = (a.x - local.x) * (a.x - local.x) + (a.y - local.y) * (a.y - local.y) + (a.z - local.z) * (a.z - local.z);
                int distb = (b.x - local.x) * (b.x - local.x) + (b.y - local.y) * (b.y - local.y) + (b.z - local.z) * (b.z - local.z);
                return dista.CompareTo(distb);
            });
            foreach (var request in requests) {
                controller.RequestChunk(request);
            }
        }


        public void OnEnable() {
            if (meshGenerator == null) {
                //meshGenerator = new ChunkMeshGeneratorCPU(OnChunkRemesh);
                meshGenerator = new ChunkMeshGeneratorGPU(marchingCubesCompute, OnChunkRemesh);
                meshGenerator.Init();
            }
        }

        private void OnDisable() {
            meshGenerator.Release();
            meshGenerator = null;
        }

        public void UpdateChunk(Chunk chunk) {
            chunks[chunk.local] = new ChunkHolder(chunk);
        }

        public void OnChunkRemesh(Chunk chunk, Mesh mesh) {
            chunks[chunk.local].mesh = mesh;
            /*if (mesh != null) {
                var obj = Instantiate(meshPreview.gameObject);
                obj.transform.position = chunk.local;
                obj.GetComponent<MeshFilter>().mesh = mesh;
                //ChunkMeshGenerator.MeshToCpu(mesh);
            }*/
        }

        private void preview() {
            var holder = chunks[Vector3Int.zero];
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

        private void Update() {
            RefreshChunks();
            
            RenderChunks();

            if (IsReady()) {
                // Do something
            }

            if (Input.GetKeyDown(KeyCode.Space)) {
                position.x += 8;
            }
        }

        private void RefreshChunks() {
            if (meshGenerator.isRemeshing) return;
            
            foreach (var (loc, holder) in chunks) {
                if (holder.hasNeighboors || holder.mesh != null) continue;
                
                holder.hasNeighboors = true;
                    
                for (int x = -1; x <= 1; x++) 
                for (int y = -1; y <= 1; y++) 
                for (int z = -1; z <= 1; z++) {
                    var key = settings.Pos(loc + new Vector3Int(x * 16, y * 16, z * 16));
                    if (!settings.IsInside(key)) continue;

                    if (!chunks.ContainsKey(key)) {
                        holder.hasNeighboors = false;
                        goto end;
                    }
                }
                end:;
                if (holder.hasNeighboors && meshGenerator.RemeshChunk(holder.chunk, settings, chunks)) {
                    break;
                }
            }
        }

        private void RenderChunks() {
            RenderParams renderParams = new RenderParams(groundMaterial);
            
            foreach (var entry in chunks) {
                var m = entry.Value.mesh;
                if (m == null) continue;
                
                var pos = settings.CloserPos(entry.Key, local);
                var matrix = Matrix4x4.Translate(pos);
                Graphics.RenderMesh(in renderParams, m, 0, matrix);
            }
        }
    }
}