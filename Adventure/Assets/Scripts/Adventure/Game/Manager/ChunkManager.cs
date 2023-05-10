using System;
using System.Collections.Generic;
using Adventure.Game.Manager.ShapeGeneration;
using Adventure.Logic.Data;
using UnityEngine;

namespace Adventure.Game.Manager {
    public class ChunkManager : MonoBehaviour {

        public Material groundMaterial;
        public ComputeShader marchingCubesCompute;
        public ComputeShader copyCompute;
        public Mesh lastMesh;

        private Dictionary<Vector3Int, Mesh> meshes = new();
        private ChunkMeshGenerator meshGenerator;
        
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
            meshGenerator.RemeshChunk(chunk);
        }

        public void OnChunkRemesh(Chunk chunk, Mesh mesh) {
            meshes[chunk.local] = mesh;
            this.lastMesh = mesh;
        }

        private void Update() {
            RenderParams renderParams = new RenderParams(groundMaterial);
            
            foreach (var mesh in meshes.Values) {
                Graphics.RenderMesh(in renderParams, mesh, 0, Matrix4x4.identity);
            }
        }
    }
}