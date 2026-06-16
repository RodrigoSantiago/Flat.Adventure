using System;
using UnityEngine;

namespace Code {
    public class Chunk {
        public const int SIZE_1 = 32;
        public const int SIZE_2 = 32 * 32;
        public const int SIZE_3 = 32 * 32 * 32;
        public const int SIZE_4 = 34 * 34 * 34;

        public readonly float[] CubeDensity = {
            0.00f, 0.10f, 0.20f, 0.30f, 0.40f, 0.45f, 
            0.55f, 0.60f, 0.65f, 0.70f, 0.75f,
            0.80f, 0.85f, 0.90f, 0.95f, 1.00f
        };
        
        public uint[] density;  // [4]
        public uint[] material; // [6]

        public float GetDensity(int x, int y, int z) {
            int voxelIndex = x + (z * SIZE_1) + (y * SIZE_2);

            int uintIndex = voxelIndex >> 3;
            int shift = (voxelIndex & 7) << 2;

            int den = (int)((density[uintIndex] >> shift) & 0xF);

            return CubeDensity[den];
        }

        public void SetDensity(int x, int y, int z, float den) {
            int voxelIndex = x + (z * SIZE_1) + (y * SIZE_2);

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
    }
}