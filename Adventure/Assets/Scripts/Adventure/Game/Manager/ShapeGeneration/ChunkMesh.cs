using Adventure.Logic.Data;
using UnityEngine;

namespace Adventure.Game.Manager.ShapeGeneration {
    public class ChunkMesh {

        public Chunk chunk;
        public int count;
        public GraphicsBuffer vertexBuffer;
        public GraphicsBuffer indexBuffer;

        public Vector3 center {
            get { return chunk.local + new Vector3Int(8, 8, 8); }
        }

        public ChunkMesh(Chunk chunk, int count, GraphicsBuffer vertexBuffer, GraphicsBuffer indexBuffer) {
            this.chunk = chunk;
            this.count = count;
            this.vertexBuffer = vertexBuffer;
            this.indexBuffer = indexBuffer;
        }

        public void Release() {
            vertexBuffer.Release();
            indexBuffer.Release();
        }
    }
}