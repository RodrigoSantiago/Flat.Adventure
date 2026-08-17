using System;
using UnityEngine;

namespace Code {
    public class TestChunkRender : MonoBehaviour {
        public ComputeShader shader;
        public MeshFilter filter;

        private ChunkMeshGenerator gen;
        
        private void Start() {
            var chunk = new Chunk();
            chunk.density = new uint[Chunk.SIZE_3 / 8];
            chunk.material = new uint[Chunk.SIZE_3 / 4];
            
            float radius = Chunk.SIZE_1 * 0.4f;
            Vector3 center = new(
                Chunk.SIZE_1 * 0.5f,
                Chunk.SIZE_1 * 0.5f,
                Chunk.SIZE_1 * 0.5f
            );
            const float aa = 1.0f;
            for (int y = 0; y < Chunk.SIZE_1; y++) {
                for (int z = 0; z < Chunk.SIZE_1; z++) {
                    for (int x = 0; x < Chunk.SIZE_1; x++) {

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

            for (int y = 0; y < Chunk.SIZE_1; y++) {
                for (int z = 0; z < Chunk.SIZE_1; z++) {
                    for (int x = 0; x < Chunk.SIZE_1; x++) {

                        float density = 0.0f;

                        if (y >= baseMargin && y < baseMargin + floorHeight) {

                            int layer = y - baseMargin; // 0..4

                            int currentMargin = baseMargin + layer;

                            bool inside =
                                x >= currentMargin &&
                                x < Chunk.SIZE_1 - currentMargin &&
                                z >= currentMargin &&
                                z < Chunk.SIZE_1 - currentMargin;

                            if (inside)
                                density = 1.0f;
                        }
                        if (density != 0)
                            chunk.SetDensity(x, y, z, density);
                    }
                }
            }
            for (int y = 0; y < 16; y++) {
                for (int z = 0; z < Chunk.SIZE_1; z++) {
                    for (int x = 0; x < Chunk.SIZE_1; x++) {
                        chunk.SetMaterial(x, y, z, 1);
                    }
                }
            }
            
            var chunkSoil = new Chunk();
            chunkSoil.density = new uint[Chunk.SIZE_3 / 8];
            chunkSoil.material = new uint[Chunk.SIZE_3 / 4];
            for (int y = 0; y < 3; y++) {
                for (int z = 0; z < Chunk.SIZE_1; z++) {
                    for (int x = 0; x < Chunk.SIZE_1; x++) {
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
                Debug.Log(period);
            });
        }
    
        public void OnDestroy() {
            gen.Release();
        }
    }
}