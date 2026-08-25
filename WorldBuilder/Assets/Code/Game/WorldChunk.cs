using Code.Data;
using Game.Data;
using UnityEngine;

namespace Code {
    public class WorldChunk {
        public Chunk chunk;
        public int LastWorkTime;

        public WorldChunk(Chunk chunk) {
            this.chunk = chunk;
        }

        public void Initialize() {
            // Create Game Objects
        }

        public void Dispose() {
            
        }
    }
}