using System;
using UnityEngine;

namespace Game.GameTest {
    [Serializable]
    public class MapSettings {
        
        public long seed;
        [Range(1, 3)]
        public int size;
        [Range(0, 1)]
        public float water;
        [Range(0, 1)]
        public float temperature;
    }
    
    [Serializable]
    public class MapGlobalSettings {
        public float minSeaLevel;
        public float maxSeaLevel;
        [Range(1, 3)]
        public int size;
        [Range(0, 1)]
        public float water;
        [Range(0, 1)]
        public float temperature;
    }
}