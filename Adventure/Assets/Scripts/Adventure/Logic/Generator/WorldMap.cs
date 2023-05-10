using System;
using System.Collections;
using Adventure.Logic.Data;
using UnityEngine;

namespace Adventure.Logic.Generator {
    
    /**
     * WorldMap class is used to manage all the world terrain generation and caching a reduced map version to allow the
     * game flow generation
     */
    public class WorldMap {
        
        public enum ContinentalType {
            OVAL, SQUARE, DOUBLE_OVAL, DOUBLE_SQUARE
        }
        
        // Chunk Creation
        private short[] readTypes = new short[256];
        private int[] tempReadTypes = new int[4096];
        private int tempReadTypeTag = 1;
        
        private short[] readBlocks = new short[4096];
        private byte[] readVolume = new byte[4096];
        
        private World world;
        private Noise noise;
        public Map Map { get; protected set; }
        
        // Noise Variables
        protected int width;
        protected int height;
        protected float[] hOctaveOffset = {452345, 345324, 523452, 187921, 987258, 802691};
        protected float[] hOctaveScale = {200, 100, 50};
        protected float[] hOctavePower = {4, 2, 1};
        protected float hOctaveSumPower;
        protected float[] iOctaveOffset = {156745, 630324, 111452, 179536, 168945, 251015};
        protected float[] iOctaveScale = {50, 25};
        protected float[] iOctavePower = {2, 1};
        protected float iOctaveSumPower;
        protected float[] wOctaveOffset = {156795, 986315, 333785, 034543, 345934, 905359};
        protected float[] wOctaveScale = {150, 37.5f};
        protected float[] wOctavePower = {4, 1};
        protected float wOctaveSumPower;
        protected float islandCutOut = 0.4f;
        protected float seaCutOut = 0.5f;
        protected float beachCutOut = 0.495f;
        protected ContinentalType continentalType = ContinentalType.SQUARE;
        protected float latTemperatureInfluence = 1.15f;
        protected float heightTemperatureInfluence = 0.75f;
        protected float seaMoistureInfluence = 0.05f;

        public WorldMap(World world, Noise noise) {
            this.world = world;
            this.noise = noise;
            InitNoise();
        }

        protected void InitNoise() {
            width = world.width;
            height = world.height;
            for (int i = 0; i < hOctaveOffset.Length; i++) {
                hOctaveOffset[i] = noise.RandomRange(Noise.SingleToInt32Bits(hOctaveOffset[i]), 0, 100);
            }
            for (int i = 0; i < iOctaveOffset.Length; i++) {
                iOctaveOffset[i] = noise.RandomRange(Noise.SingleToInt32Bits(iOctaveOffset[i]), 0, 100);
            }
            for (int i = 0; i < wOctaveOffset.Length; i++) {
                wOctaveOffset[i] = noise.RandomRange(Noise.SingleToInt32Bits(wOctaveOffset[i]), 0, 100);
            }
            foreach (var p in hOctavePower) {
                hOctaveSumPower += p;
            }
            foreach (var p in iOctavePower) {
                iOctaveSumPower += p;
            }
            foreach (var p in wOctavePower) {
                wOctaveSumPower += p;
            }
        }

        public IEnumerator GenerateMap() {
            const int level = 1;
            
            Map map = new Map();
            map.level = level;
            map.geology = new float[width / level * height / level];
            map.moisture = new float[width / level * height / level];
            map.temperature = new float[width / level * height / level];
            for (int y = 0; y < height / level; y++) {
                for (int x = 0; x < width / level; x++) {
                    int i = y * width + x;
                    GenerateMapValues(x * level, y * level, out map.geology[i], out map.moisture[i], out map.temperature[i]);
                }

                yield return null;
            }

            Map = map;
        }

        private float Lerp(float a, float b, float t) {
            return a * (1 - t) + b * t;
        }

        private float Clamp01(float val) {
            return val < 0 ? 0 : val > 1 ? 1 : val;
        }
        
        public void GenerateMapValues(int x, int y, out float geology, out float moisture, out float temperature) {

            float f = 1;
            float px = x / (float) width;
            float py = y / (float) height;
            if (continentalType == ContinentalType.OVAL) {
                f = MathF.Sqrt((px - 0.5f) * (px - 0.5f) + (py - 0.5f) * (py - 0.5f)) * 2;
            } else if (continentalType == ContinentalType.SQUARE) {
                f = MathF.Abs(px - 0.5f) * 2;
            } else if (continentalType == ContinentalType.DOUBLE_SQUARE) {
                f = MathF.Min(
                    MathF.Max(MathF.Abs(px * 2 - 0.5f), MathF.Abs(py - 0.5f)),
                    MathF.Max(MathF.Abs(px * 2 - 1.5f), MathF.Abs(py - 0.5f))) * 2f;
            } else if (continentalType == ContinentalType.DOUBLE_OVAL) {
                f = MathF.Sqrt(MathF.Min(
                        (px * 2 - 0.5F) * (px * 2 - 0.5F) + (py - 0.5F) * (py - 0.5F),
                        (px * 2 - 1.5F) * (px * 2 - 1.5F) + (py - 0.5F) * (py - 0.5F)
                    )
                ) * 2f;
            }

            f = Interpolation.Exp5In(f);

            // Geology
            float o1 = (float) noise.Wave2D(x / hOctaveScale[0] + hOctaveOffset[0], y / hOctaveScale[0] + hOctaveOffset[1]) * hOctavePower[0];
            float o2 = (float) noise.Wave2D(x / hOctaveScale[1] + hOctaveOffset[2], y / hOctaveScale[1] + hOctaveOffset[3]) * hOctavePower[1];
            float o3 = (float) noise.Wave2D(x / hOctaveScale[2] + hOctaveOffset[4], y / hOctaveScale[2] + hOctaveOffset[5]) * hOctavePower[2];
            float g = (o1 + o2 + o3) / hOctaveSumPower;
            float gH = (g * 0.5f + 0.5f);
            float gW = gH < seaCutOut ? gH : (gH - seaCutOut) * (1 - islandCutOut * 2f) + seaCutOut;

            // Islands / Montains
            float i1 = (float) noise.Wave2D(x / iOctaveScale[0] + iOctaveOffset[0], y / iOctaveScale[0] + iOctaveOffset[1]) * iOctavePower[0];
            float i2 = (float) noise.Wave2D(x / iOctaveScale[1] + iOctaveOffset[2], y / iOctaveScale[1] + iOctaveOffset[3]) * iOctavePower[1];
            float i = (i1 + i2) / iOctaveSumPower;
            i = (i * 0.5f + 0.5f);
            
            // TODO - Vulcans (The Height increase the temperature)

            // Ground Mask
            float groundMask = Clamp01(gH - beachCutOut) / (1 - beachCutOut);
            groundMask = Interpolation.Exp10Out(Interpolation.Exp5Out(groundMask));

            // Sea Mask
            float seaMask = 1 - (Clamp01(gH + (1 - seaCutOut)) - (1 - seaCutOut)) / seaCutOut;
            seaMask = Interpolation.Exp10Out(Interpolation.Exp5Out(seaMask));

            // Wet Mask
            float wetMask = 1 - (Clamp01(gH - seaCutOut) / (1 - seaCutOut));

            // Island Earth
            float iE = Interpolation.Smooth(i);

            // Island Sea
            float iI = Interpolation.Circle(Interpolation.Exp5In(i));

            // Final Height
            float h = gW
                      + (iE * groundMask * islandCutOut)
                      + (seaMask * iI * (1 - seaCutOut));
            h = Lerp(h, beachCutOut / 2f, f);

            // Latitude
            float lat = MathF.Abs(y / (float) height - 0.5f) * 2.0f;

            // Cold
            float lCold = Interpolation.Pow2OutInverse(lat) * latTemperatureInfluence;
            float hCold = (h < seaCutOut ? (1 - h / seaCutOut) * 0.5f : (h - seaCutOut) / (1 - seaCutOut)) * heightTemperatureInfluence;

            // Final Cold
            float c = Clamp01(hCold + lCold); // COLD

            // Wet
            float m1 = (float) noise.Wave2D(x / wOctaveScale[0] + wOctaveOffset[0], y / wOctaveScale[0] + wOctaveOffset[1]) * wOctavePower[0];
            float m2 = (float) noise.Wave2D(x / wOctaveScale[1] + wOctaveOffset[2], y / wOctaveScale[1] + wOctaveOffset[3]) * wOctavePower[0];
            float m = (m1 + m2) / wOctaveSumPower;
            m = Interpolation.Smooth(m * 0.5f + 0.5f);
            float w = h < beachCutOut ? 1 : Lerp(m, wetMask, seaMoistureInfluence); // WET

            geology = h;
            moisture = w;
            temperature = 1 - c;
        }

        public Chunk GenerateChunk(Vector3Int local) {
            Voxel[] voxels = new Voxel[16 * 16 * 16];
            for (int x = 0; x < 16; x++)
            for (int y = 0; y < 16; y++)
            for (int z = 0; z < 16; z++) {
                if (y > 8) {
                    voxels[x * 256 + y * 16 + z] = new Voxel(1, 1f);
                } else {
                    voxels[x * 256 + y * 16 + z] = new Voxel(0, 0f);
                }
            }

            return new Chunk(local, voxels, true);


            tempReadTypeTag++;
            
            int usedTypes = 0;
            bool usePalette = true;
            for (int x = 0; x < 16; x++)
            for (int y = 0; y < 16; y++) {
                int offSet = x * 256 + y * 16;
                GenerateLine(local, x, y, offSet, readBlocks, readVolume);

                if (!usePalette) continue;

                for (int i = 0; i < 16; i++) {
                    if (tempReadTypes[readBlocks[i + offSet]] != tempReadTypeTag) {
                        tempReadTypes[readBlocks[i + offSet]] = tempReadTypeTag;
                        if (usedTypes < 256) {
                            readTypes[usedTypes] = readBlocks[i + offSet];
                            usedTypes++;
                        } else {
                            usePalette = false;
                        }
                    }
                }
            }

            Chunk chunk = null;//new Chunk(16, local, true, palette, readBlocks, readVolume);


            return chunk;
        }
        
        public void GenerateLine(Vector3Int local, int x, int y, int offset, short[] blocks, byte[] volume) {
            Vector3Int position = new Vector3Int(local.x * 16 + x, local.y * 16 + y, local.z * 16);
            GenerateMapValues(position.x, position.y, out var g, out var m, out var t);

            Biome biome = FindBiome(x, y, g, m, t);
            for (int i = 0; i < 16; i++) {
                Voxel vox = biome.Generate(new Vector3Int(position.x, position.y, position.z + i), g, m, t);
                blocks[offset] = vox.material;
                volume[offset++] = vox.volume;
            }
        }

        public Biome FindBiome(int x, int y, float g, float m, float t) {
            return null;
        }
    }
}