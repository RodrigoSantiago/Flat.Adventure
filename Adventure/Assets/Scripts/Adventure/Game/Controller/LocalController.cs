using Adventure.Game.Manager;
using Adventure.Logic;
using Adventure.Logic.Data;
using UnityEngine;

namespace Adventure.Game.Controller {
    public class LocalController : WorldPlayerController {

        public readonly ChunkManager chunkManager;
        public readonly UnitManager unitManager;

        public LocalController(string name, ChunkManager chunkManager, UnitManager unitManager) {
            this.name = name;
            this.chunkManager = chunkManager;
            this.unitManager = unitManager;
        }

        public void RequestChunk(Vector3Int local) {
            
        }

        public override void OnChunkReceived(Chunk chunk) {
            chunkManager.UpdateChunk(chunk);
        }
    }
}
