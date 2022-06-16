using System.Collections.Generic;

namespace Adventure.Logic {
    public abstract class Biome {
        public abstract Block[] Blocks { get; protected set; }

        public abstract Palette[] CommonPalettes { get; protected set; }

        protected Biome() {
            
        }
        
        public virtual Voxel Generate(Point3 position) {
            return default;
        }
    }
}