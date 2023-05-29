using Adventure.Logic.Data;
using UnityEngine;

namespace Adventure.Game.Manager.ChunkManagment {
    public class ChunkHolder {
        private static readonly int[] size = {8, 64, 512};
        
        public readonly int lod;
        
        public Chunk chunk { get; private set; }
        public Mesh mesh { get; private set; }
        public Mesh lodMesh { get; private set; }
        public bool ready { get; private set; }
        public bool lodReady { get; private set; }
        public bool hasNeighboors;
        
        public ChunkHolder(int lod) {
            this.lod = lod;
        }

        public void ChunkReady(Chunk chunk) {
            this.chunk = chunk;
        }
        
        public void MeshLodReady(Mesh lodMesh) {
            this.lodMesh = lodMesh;
            this.lodReady = true;
        }
        
        public void MeshReady(Mesh mesh) {
            this.mesh = mesh;
            this.ready = true;
        }
    }
}