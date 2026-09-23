using System;
using Game.Data;

namespace Game.Worlds.Rendering {
    public struct LodConfig {
        public IndexPos ViewLod;
        public IndexPos Max;
        public int ChunkSize;
        public int Size;
        public int Height;
        public bool Cylinder;
        private float r2, cx, cz;

        public IndexPos Center => (ViewLod + Max) / 2;
            
        public LodConfig(IndexPos viewLod, int chunkSize, int size, int height, bool cylinder) {
            ViewLod = viewLod;
            ChunkSize = chunkSize;
            Size = size;
            Height = height;
            Cylinder = cylinder;

            float radius = (size * 0.5f + 0.01f) * ChunkSize;
            r2 = radius * radius;
            cx = ViewLod.x + (size * 0.5f) * ChunkSize;
            cz = ViewLod.z + (size * 0.5f) * ChunkSize;
            Max = ViewLod + new IndexPos(size, height, size) * ChunkSize;
            Max.y = Math.Min(Max.y, Region.MaxHeight);
        }

        public bool IsInside(IndexPos pos) {
            if (pos.y < ViewLod.y || pos.y >= Max.y) return false;

            if (Cylinder) {
                float dx = pos.x - cx;
                float dz = pos.z - cz;

                return (dx * dx + dz * dz) <= r2;
            }

            return pos.x >= ViewLod.x && pos.x < Max.x &&
                   pos.z >= ViewLod.z && pos.z < Max.z;
        }
        
        public bool IsInside(IndexPos pos, int size) {
            if (pos.y + size <= ViewLod.y || pos.y >= Max.y)
                return false;
            
            if (pos.x + size <= ViewLod.x || pos.x >= Max.x)
                return false;
            
            if (pos.z + size <= ViewLod.z || pos.z >= Max.z)
                return false;

            return true;
        }
        
        public LodConfig Padding() {
            return new LodConfig(ViewLod + ChunkSize * 2, ChunkSize, Size - 4, Height - 4, false);
        }
    }
}