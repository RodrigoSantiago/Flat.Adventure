using System;
using System.Collections.Generic;
using Code;
using Game.Data;
using Game.GraphicGenerator;
using UnityEngine;

namespace Game.GameTest {
    public class TestChunkRender : MonoBehaviour {
        
        public MeshFilter filter;
        [SerializeField] private ChunkMeshGenerator gen;

        ChunkSoil chunk;
        ChunkSoil chunkSoil;
        
        private void Start() {
            chunk = new ChunkSoil();
            
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
            
            chunkSoil = new ChunkSoil();
            for (int y = 0; y < 3; y++) {
                for (int z = 0; z < ChunkSoil.Size1D; z++) {
                    for (int x = 0; x < ChunkSoil.Size1D; x++) {
                        chunkSoil.SetDensity(x, y, z, 1.0f);
                        chunk.SetDensity(x, y, z, 1.0f);
                    }
                }
            }

            DateTime start = DateTime.Now;
            
            /*gen.SimpleMesh(new IndexPos(99, 99, 99), chunk, (mesh) => {
                filter.sharedMesh = mesh;
                var period = DateTime.Now.Subtract(start);
                Debug.Log(period);
                // Stress();
            });*/
        }

        public List<Mesh> meshes = new();

        public void Stress() {
            DateTime start = DateTime.Now;
            for (int i = 0; i < 99; i++) {
                gen.SimpleMesh(new IndexPos(99 + i, 99 + i, 99 + i), chunk, (mesh) => {
                    meshes.Add(mesh);
                });
            }

            gen.SimpleMesh(new IndexPos(200, 200, 200), chunk, (mesh) => {
                filter.sharedMesh = mesh;
                meshes.Add(mesh);
                var period = DateTime.Now.Subtract(start);
                Debug.Log("Last");
                Debug.Log(period);
            });
        }
    
        public void OnDestroy() {
            gen.Release();
        }
    }
}