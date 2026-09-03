using Code;
using Game.Data;
using Game.GraphicGenerator;
using Game.Worlds;
using UnityEngine;

namespace Game.Entities {
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class CvChunk : MonoBehaviour {
        private WorldManager manager;
        public Chunk chunk;

        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;

        private void Awake() {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            var b = gameObject.AddComponent<BoxCollider>();
            b.size = new Vector3(32, 32, 32);
            b.center = new Vector3(16, 16 ,16);
        }
        
        public void Setup(WorldManager manager, Chunk chunk) {
            this.manager = manager;
            this.chunk = chunk;
            transform.position = new Vector3(this.chunk.Pos.x, this.chunk.Pos.y, this.chunk.Pos.z);
            transform.localScale = Vector3.one * (1 << chunk.Lod);
            meshFilter.sharedMesh = this.chunk.SoilMesh;
            meshRenderer.material = ChunkMeshGenerator.Instance.GroundMaterial;
        }

        public void RequestMesh() {
            Chunk[] chunks = new Chunk[27];
            
            int i = 0;
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
            for (int x = -1; x <= 1; x++) {
                var pos = chunk.Pos + new IndexPos(x, y, z) * (32 << chunk.Lod);
                if (pos.x < 0 || pos.y < 0 || pos.z < 0) {
                    chunks[i++] = null;
                } else {
                    var near = manager.FindChunk(chunk.Lod, pos);
                    if (near == null) {
                        return; // Missing non null
                    }

                    chunks[i++] = near.chunk;
                }
            }

            chunks[13] = chunk;
            GameManager.Instance.RequestMesh(chunks, (mesh) => {
                chunk.SoilMesh = mesh;
                meshFilter.sharedMesh = mesh;
            });
        }
    }
}