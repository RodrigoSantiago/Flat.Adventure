using System;

namespace Game.Data {
    public class ChunkSoil {
        public const int Size1D = 32;
        public const int Size2D = 32 * 32;
        public const int Size3D = 32 * 32 * 32;
        public const int Size4D = 34 * 34 * 34; // Padding Chunks

        public static readonly float[] CubeDensity = {
            0.00f, 0.10f, 0.20f, 0.30f, 0.40f, 0.45f,
            0.55f, 0.60f, 0.65f, 0.70f, 0.75f,
            0.80f, 0.85f, 0.90f, 0.95f, 1.00f
        };

        public uint[] density;  // [4]
        public uint[] material; // [6]

        public float GetDensity(int x, int y, int z) {
            int voxelIndex = x + (z * Size1D) + (y * Size2D);

            int uintIndex = voxelIndex >> 3;
            int shift = (voxelIndex & 7) << 2;

            int den = (int)((density[uintIndex] >> shift) & 0xF);

            return CubeDensity[den];
        }

        public void SetDensity(int x, int y, int z, float den) {
            int voxelIndex = x + (z * Size1D) + (y * Size2D);

            int value = 0;
            float best = float.MaxValue;

            for (int i = 0; i < CubeDensity.Length; i++) {
                float diff = MathF.Abs(CubeDensity[i] - den);
                if (diff < best) {
                    best = diff;
                    value = i;
                }
            }

            int uintIndex = voxelIndex >> 3;
            int shift = (voxelIndex & 7) << 2;

            uint mask = (uint)(0xFu << shift);

            density[uintIndex] = (density[uintIndex] & ~mask) | ((uint)value << shift);
        }

        public int GetMaterial(int x, int y, int z) {
            int voxelIndex = x + (z * Size1D) + (y * Size2D);
            
            int uintIndex = voxelIndex >> 2;
            int shift = (voxelIndex & 3) << 3;
            return (int)((material[uintIndex] >> shift) & 0xFF);
        }

        public void SetMaterial(int x, int y, int z, int value) {
            int voxelIndex = x + (z * Size1D) + (y * Size2D);
            
            int uintIndex = voxelIndex >> 2;
            int shift = (voxelIndex & 3) << 3;
            uint mask = 0xFFu << shift;
            material[uintIndex] = (material[uintIndex] & ~mask) | ((uint)value << shift);
        }
    }
}