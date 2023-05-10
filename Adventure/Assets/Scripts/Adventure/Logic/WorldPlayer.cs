using UnityEngine;

namespace Adventure.Logic {
    public class WorldPlayer {

        public readonly int id;
        public readonly string name;
        public readonly WorldPlayerController controller;
        public Vector3Int spawnPoint;
        
        public Unit unit { get; set; }
        public World world { get; set; }

        public WorldPlayer(WorldPlayerController controller, string name) {
            this.id = 0;
            this.name = name;
            this.controller = controller;
        }

        public void Spawn() {
            // World.AddUnit();
            // World.RequestChunkGroup()
            var chunk = world.LoadChunk(new Vector3Int(0, 0, 0));
            controller.OnChunkReceived(chunk);
            
        }

        public void Vanish() {
            // World.RemoveUnit();
        }
        
        public void Update() {
            
        }
    }
}