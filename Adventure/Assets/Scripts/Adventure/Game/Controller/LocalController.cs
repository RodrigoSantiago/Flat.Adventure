using Adventure.Game.Manager;
using Adventure.Logic;
using Adventure.Logic.Data;
using UnityEngine;

namespace Adventure.Game.Controller {
    public class LocalController : WorldPlayerController {
        
        public World world { get; set; }
        public readonly ChunkManager chunkManager;
        public readonly UnitManager unitManager;

        public LocalController(string name, ChunkManager chunkManager, UnitManager unitManager) {
            this.name = name;
            this.chunkManager = chunkManager;
            this.unitManager = unitManager;
        }

        public override void RequestChunk(Vector3Int local, int lod) {
            world.RequestChunk(local, this, lod);
        }

        public override void OnChunkReceived(Chunk chunk) {
            chunkManager.OnChunkReceived(chunk);
        }
    }
}
