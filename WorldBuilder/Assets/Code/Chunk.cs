using System;
using UnityEngine;

namespace Code {
    public class Chunk {
        public const int SIZE_1 = 32;
        public const int SIZE_2 = 32 * 32;
        public const int SIZE_3 = 32 * 32 * 32;

        public readonly float[] CubeDensity = {
            0.00f, 0.10f, 0.20f, 0.30f, 0.40f, 0.45f, 
            0.55f, 0.60f, 0.65f, 0.70f, 0.75f,
            0.80f, 0.85f, 0.90f, 0.95f, 1.00f
        };
        
        public byte[] density;  // [4]
        public byte[] material; // [6]

        public float GetDensity(int x, int y, int z) {
            int den = density[(x + (y * SIZE_2) + (z * SIZE_1)) / 2];
            if ((x & 0x1) == 0x1) {
                return CubeDensity[(den & 0b11110000) >> 4];
            }
            return CubeDensity[den & 0b1111];
        }

        public void SetDensity(int x, int y, int z, float den) {
            int index = (x + (y * SIZE_2) + (z * SIZE_1)) / 2;

            int value = 0;
            float best = float.MaxValue;

            for (int i = 0; i < CubeDensity.Length; i++) {
                float diff = MathF.Abs(CubeDensity[i] - den);
                if (diff < best) {
                    best = diff;
                    value = i;
                }
            }

            byte current = density[index];
            if ((x & 1) == 1) {
                density[index] = (byte)((current & 0x0F) | (value << 4));
            } else {
                density[index] = (byte)((current & 0xF0) | value);
            }
        }

        public int GetMaterial(int x, int y, int z) {
            return material[(x + (y * SIZE_2) + (z * SIZE_1)) / 2];
        }
    }
}