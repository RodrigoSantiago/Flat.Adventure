using Game.Data;

namespace Game.Worlds.Generation {
    public class WorldHeightMap {
        private byte[] data;
        public WorldHeightMap(byte[] data) {
            this.data = data;
        }
        
        public static readonly float[] CubeDensity = {
            0.00f, 0.10f, 0.20f, 0.30f, 0.40f,
            0.55f, 0.60f, 0.65f, 0.70f, 0.75f,
            0.80f, 0.85f, 0.90f, 0.95f, 1.00f, 2.00f
        };

        public float GetMaxHeight(IndexPos region) {
            region = region.GetChunkIndex(Region.MaxLod);
            int index = region.x / 256 + (region.z / 256) * 64;
    
            byte a = data[index * 2];
            byte b = data[index * 2 + 1];
    
            ushort packedValue = (ushort)(a | (b << 8));
            int heightInt = (packedValue >> 4) & 0x3FF;
            int subLevel = packedValue & 0x0F; 
            
            return heightInt + CubeDensity[subLevel];
        }
    }
}