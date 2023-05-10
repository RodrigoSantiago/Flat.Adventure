using Adventure.Logic.Data;
using UnityEngine;

namespace Adventure.Logic.Generator {
    public abstract class Biome {
        public abstract Block[] Blocks { get; protected set; }

        protected Biome() {
            
        }
        
        public virtual Voxel Generate(Vector3Int position, float g, float m, float t) {
            return default;
        }
    }
}