using System;
using Code.Data;
using Game.Data;
using UnityEngine;

namespace Code {
    public class TestChunkRender : MonoBehaviour {
        public ComputeShader shader;
        public MeshFilter filter;

        private ChunkMeshGenerator gen;
        
        private void Start() {
            var chunk = new ChunkSoilPacked();
            
            float radius = ChunkSoil.Size1D * 0.4f;
            Vector3 center = new(
                ChunkSoil.Size1D * 0.5f,
                ChunkSoil.Size1D * 0.5f,
                ChunkSoil.Size1D * 0.5f
            );
            const float aa = 1.0f;
            for (int y = 0; y < ChunkSoil.Size1D; y++) {
                for (int z = 0; z < ChunkSoil.Size1D; z++) {
                    for (int x = 0; x < ChunkSoil.Size1D; x++) {

                        Vector3 p = new(x, y, z);

                        float sdf = (p - center).magnitude - radius;

                        float density = Mathf.Clamp01(
                            0.5f - sdf / (aa * 2.0f)
                        );

                        chunk.SetDensity(x, y, z, density);
                    }
                }
            }
            
            const int baseMargin = 2;
            const int floorHeight = 5;

            for (int y = 0; y < ChunkSoil.Size1D; y++) {
                for (int z = 0; z < ChunkSoil.Size1D; z++) {
                    for (int x = 0; x < ChunkSoil.Size1D; x++) {

                        float density = 0.0f;

                        if (y >= baseMargin && y < baseMargin + floorHeight) {

                            int layer = y - baseMargin; // 0..4

                            int currentMargin = baseMargin + layer;

                            bool inside =
                                x >= currentMargin &&
                                x < ChunkSoil.Size1D - currentMargin &&
                                z >= currentMargin &&
                                z < ChunkSoil.Size1D - currentMargin;

                            if (inside)
                                density = 1.0f;
                        }
                        if (density != 0)
                            chunk.SetDensity(x, y, z, density);
                    }
                }
            }
            for (int y = 0; y < 8; y++) {
                for (int z = 0; z < ChunkSoil.Size1D; z++) {
                    for (int x = 0; x < ChunkSoil.Size1D; x++) {
                        chunk.SetMaterial(x, y, z, 1);
                    }
                }
            }
            
            var chunkSoil = new ChunkSoilPacked();
            for (int y = 0; y < 3; y++) {
                for (int z = 0; z < ChunkSoil.Size1D; z++) {
                    for (int x = 0; x < ChunkSoil.Size1D; x++) {
                        chunkSoil.SetDensity(x, y, z, 15);
                        chunk.SetDensity(x, y, z, 15);
                    }
                }
            }
            
            DateTime start = DateTime.Now;
            gen = new ChunkMeshGenerator(shader);
            gen.Init();
            gen.Remesh(chunk, chunkSoil, (mesh) => {
                filter.sharedMesh = mesh;
                var period = DateTime.Now.Subtract(start);
                start = DateTime.Now;
                gen.Remesh(chunk, chunkSoil, (mesh2) => {
                    filter.sharedMesh = mesh2;
                    var period2 = DateTime.Now.Subtract(start);
                    Debug.Log(period2);
                });
            });
        }
    
        public void OnDestroy() {
            gen.Release();
        }
    }
}