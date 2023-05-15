using UnityEngine;

namespace Adventure.Logic {
    public class WorldPlayer {
        
        public int minViewSize = 2;
        public int maxViewSize = 2;

        public readonly int id;
        public readonly string name;
        public readonly WorldPlayerController controller;
        public Vector3Int spawnPoint;
        
        public Unit unit { get; set; }
        public World world { get; set; }
        public Vector3 position = new Vector3(64, 0, 64);
        public Vector3Int local {
            get {
                return new Vector3Int(
                    Mathf.FloorToInt(position.x / 16) * 16,
                    Mathf.FloorToInt(position.y / 16) * 16,
                    Mathf.FloorToInt(position.z / 16) * 16
                );
            }
        }

        private bool ready;
        private Vector3Int prevLocal;
        private Vector3Int prevReadyLocal;

        public WorldPlayer(WorldPlayerController controller, string name) {
            this.id = 0;
            this.name = name;
            this.controller = controller;
        }

        public void Spawn() {
            // World.AddUnit();
            // World.RequestChunkGroup()
        }

        public void Vanish() {
            // World.RemoveUnit();
        }
        
        public void Update() {
            if (prevLocal != local) {
                prevLocal = local;
                ready = IsReadyToPlay();
            }
            
            if (!ready) {
                ready = IsReadyToPlay();
                if (!ready && prevReadyLocal != local) {
                    prevReadyLocal = local;
                    RequestToPlay();
                }
            } else {
                
            }
        }
        
        public bool IsReadyToPlay() {
            Vector3Int loc = local;
            for (int x = -minViewSize; x < minViewSize; x++) {
                int y = 0;
                for (int z = -minViewSize; z < minViewSize; z++) {
                    var key = world.settings.Pos(loc + new Vector3Int(x * 16, y * 16, z * 16));
                    if (!world.settings.IsInside(key)) continue;
                    if (!world.IsChunkReady(key)) return false;
                }
            }

            return true;
        }
        
        public void RequestToPlay() {
            Vector3Int loc = local;
            for (int x = -minViewSize; x < minViewSize; x++) {
                int y = 0;
                for (int z = -minViewSize; z < minViewSize; z++) {
                    var key =  world.settings.Pos(loc + new Vector3Int(x * 16, y * 16, z * 16));
                    if (!world.settings.IsInside(key)) continue;
                    if (!world.IsChunkForReady(key)) world.RequestChunk(key);
                }
            }
        }
    }
}