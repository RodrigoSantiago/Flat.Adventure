using System;
using UnityEngine;

namespace Code {
    public class TestChunkRender : MonoBehaviour {
        public ComputeShader shader;
        public MeshFilter filter;

        private ChunkMeshGenerator gen;
        
        private void Start() {
            byte[] chuckData = new byte[Chunk.SIZE_3 / 2];
            var chunk = new Chunk();
            chunk.density = chuckData;
            
            float radius = Chunk.SIZE_1 * 0.4f;
            Vector3 center = new(
                Chunk.SIZE_1 * 0.5f,
                Chunk.SIZE_1 * 0.5f,
                Chunk.SIZE_1 * 0.5f
            );
            /*const float aa = 1.0f;
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
            }*/
            
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

                        chunk.SetDensity(x, y, z, density);
                    }
                }
            }
            gen = new ChunkMeshGenerator(shader);
            gen.Init();
            gen.Remesh(chunk, (mesh) => {
                filter.sharedMesh = mesh;
            });
        }
    
        public void OnDestroy() {
            gen.Release();
        }
    }
}