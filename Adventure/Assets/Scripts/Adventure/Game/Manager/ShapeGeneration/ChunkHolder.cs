using Adventure.Logic.Data;
using UnityEngine;

namespace Adventure.Game.Manager.ShapeGeneration {
    public class ChunkHolder {

        public bool hasNeighboors;
        public Chunk chunk;
        public Mesh mesh;

        public ChunkHolder(Chunk chunk) {
            this.chunk = chunk;
        }
    }
}