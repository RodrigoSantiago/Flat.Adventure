using System;
using System.Collections.Generic;
using Adventure.Game.Manager.ShapeGeneration;
using Adventure.Logic;
using Adventure.Logic.Data;
using UnityEngine;

namespace Adventure.Game.Manager {
    
    public class ChunkManager : MonoBehaviour {

        public Material groundMaterial;
        public ComputeShader marchingCubesCompute;
        public MeshFilter meshPreview;

        private List<Vector3Int> chunkRequest = new();
        private Dictionary<Vector3Int, ChunkHolder> chunks = new();
        private ChunkMeshGenerator meshGenerator;

        public WorldPlayerController controller;
        
        public int minViewSize = 2;
        public int maxViewSize = 2;
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
            for (int x = -minViewSize; x < minViewSize; x++) {
                int y = 0;
                for (int z = -minViewSize; z < minViewSize; z++) {
                    var key = loc + new Vector3Int(x * 16, y, z * 16);
                    if (!chunks.ContainsKey(key)) {
                        return false;
                    }
                }
            }

            return true;
        }
        
        private void RequestToPlay() {
            Vector3Int loc = local;
            for (int x = -minViewSize; x < minViewSize; x++) {
                int y = 0;
                for (int z = -minViewSize; z < minViewSize; z++) {
                    var key = loc + new Vector3Int(x * 16, y, z * 16);
                    if (!chunks.ContainsKey(key) && !chunkRequest.Contains(key)) {
                        chunkRequest.Add(key);
                        controller.RequestChunk(key);
                    }
                }
            }
        }
        

        public void OnEnable() {
            if (meshGenerator == null) {
                meshGenerator = new ChunkMeshGenerator(marchingCubesCompute, OnChunkRemesh);
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

        private bool first;
        public void OnChunkRemesh(Chunk chunk, Mesh mesh) {
            chunks[chunk.local].mesh = mesh;
            if (mesh != null) {
                var obj = Instantiate(meshPreview.gameObject);
                obj.transform.position = chunk.local;
                obj.GetComponent<MeshFilter>().mesh = mesh;
                ChunkMeshGenerator.MeshToCpu(mesh);
            }
        }

        private void Update() {
            
            RefreshChunks();
            
            RenderChunks();

            if (IsReady()) {
                // Do something
            }

            if (Input.GetKeyDown(KeyCode.Space)) {
                foreach (var value in chunks.Values) {
                    string s = "";
                    
                    for (int y = 0; y < 16; y++) {
                        for (int x = 0; x < 16; x++) {
                            s += value.chunk[x, y, 1].Volume.ToString("0.000")+" ";
                        }

                        s += "\n";
                    }
                    Debug.Log(s);
                    break;
                }
            }
        }

        private void RefreshChunks() {
            if (meshGenerator.isRemeshing) return;
            
            foreach (var (loc, holder) in chunks) {
                if (holder.hasNeighboors || holder.mesh != null) continue;
                
                holder.hasNeighboors = true;
                    
                for (int x = -1; x <= 1; x++) 
                for (int y = 0; y <= 0; y++) 
                for (int z = -1; z <= 1; z++) {
                    var key = loc + new Vector3Int(x * 16, y * 16, z * 16);
                    if (key.x < 0 || key.y < 0 || key.z < 0) continue;

                    if (!chunks.ContainsKey(key)) {
                        holder.hasNeighboors = false;
                        goto end;
                    }
                }
                end:;
            }

            foreach (var holder in chunks.Values) {
                if (holder.hasNeighboors && holder.mesh == null) {
                    meshGenerator.RemeshChunk(holder.chunk, chunks);
                    break;
                }
            }
        }

        private void RenderChunks() {
            RenderParams renderParams = new RenderParams(groundMaterial);

            foreach (var entry in chunks) {
                var m = entry.Value.mesh;
                if (m != null) {
                    var matrix = Matrix4x4.Translate(entry.Key);
                    Graphics.RenderMesh(in renderParams, m, 0, matrix);
                }
            }
        }
    }
}