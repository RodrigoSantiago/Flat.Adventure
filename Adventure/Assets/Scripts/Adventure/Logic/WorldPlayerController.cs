using Adventure.Logic.Data;
using UnityEngine;

namespace Adventure.Logic {
    public abstract class WorldPlayerController {
        
        public string name { get; protected set; }
        
        public WorldPlayer player { get; internal set; }
        
        public abstract void OnChunkReceived(Chunk chunk);
    }
}
