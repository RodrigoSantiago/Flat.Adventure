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
        public bool hasNeighboors;
        private int lodReadyInt;
        
        public ChunkHolder(int lod) {
            this.lod = lod;
        }

        public void ChunkReady(Chunk chunk) {
            this.chunk = chunk;
        }
        
        public void MeshLodReady(Mesh lodMesh, Vector3Int minLimit, Vector3Int maxLimit) {
            this.lodMesh = lodMesh;
            lodReadyInt = (minLimit.x << 0) | (minLimit.y << 1) | (minLimit.z << 2) |
                          (maxLimit.x << 3) | (maxLimit.y << 4) | (maxLimit.z << 5);
        }
        
        public void MeshReady(Mesh mesh) {
            this.mesh = mesh;
            this.ready = true;
        }

        public bool IsLodReady(Vector3Int minLimit, Vector3Int maxLimit) {
            int t = (minLimit.x << 0) | (minLimit.y << 1) | (minLimit.z << 2) |
                          (maxLimit.x << 3) | (maxLimit.y << 4) | (maxLimit.z << 5);
            return t == lodReadyInt;
        }
    }
}