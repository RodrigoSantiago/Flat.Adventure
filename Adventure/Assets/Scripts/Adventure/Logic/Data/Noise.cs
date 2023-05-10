using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Adventure.Logic.Data {
    public class Noise {
        
        private long seed;
        private int subSeed;
        private byte[] perm;
        private byte[] perm2D;
        private byte[] perm3D;
        private float[] wave1D;

        public Noise(long seed) {
            this.seed = seed;
            perm = new byte[256];
            perm2D = new byte[256];
            perm3D = new byte[256];
            wave1D = new float[256];
            var source = new byte[256];
            for (int i = 0; i < 256; i++) {
                source[i] = (byte)i;
            }
            seed = seed * 6364136223846793005L + 1442695040888963407L;
            seed = seed * 6364136223846793005L + 1442695040888963407L;
            seed = seed * 6364136223846793005L + 1442695040888963407L;
            for (int i = 255; i >= 0; i--) {
                seed = seed * 6364136223846793005L + 1442695040888963407L;
                int r = (int)((seed + 31) % (i + 1));
                if (r < 0) {
                    r += (i + 1);
                }

                wave1D[i] = RandomRange((int) seed, -1, 1);
                perm[i] = source[r];
                perm2D[i] = (byte)(perm[i] & 0x0E);
                perm3D[i] = (byte)((perm[i] % 24) * 3);
                source[r] = source[i];
            }
        }

        public void RandomReset(int subSeed) {
            this.subSeed = subSeed;
        }

        public float NextRandom() {
            subSeed = (int) ((ulong) subSeed * 48271 % 0x7fffffff);
            return Random(subSeed);
        }

        public float Random(int entry) {
            return (float)RandomInt(entry) / int.MaxValue;
        }

        public int RandomInt(int entry) {
            long bit = seed + ShuffleEntry(entry);
            return (int)((bit * 1103515245 + 12345) & 0x7fffffff);
        }
        
        public float RandomRange(int entry, float start, float endInclusive) {
            float rand = Random(entry);
            return rand * (endInclusive - start) + start;
        }
        
        public int RandomRangeInt(int entry, int start, int endInclusive) {
            int rand = (int) MathF.Floor(RandomRange(entry, start, endInclusive + 1));
            return rand > endInclusive ? endInclusive : rand;
        }

        public double Wave1D(double x) {
            var lo = FastFloor(x);
            var hi = lo + 1;
            var dist = x - lo;
            var loSlope = wave1D[lo % 256];
            var hiSlope = wave1D[hi % 256];
            var loPos = loSlope * dist;
            var hiPos = -hiSlope * (1 - dist);
            var u = dist * dist * (3.0 - 2.0 * dist);
            return (loPos * (1 - u)) + (hiPos * u);
        }

        public double Wave2D(double x, double y) {
            var stretchOffset = (x + y) * STRETCH_2D;
            var xs = x + stretchOffset;
            var ys = y + stretchOffset;

            var xsb = FastFloor(xs);
            var ysb = FastFloor(ys);

            var squishOffset = (xsb + ysb) * SQUISH_2D;
            var dx0 = x - (xsb + squishOffset);
            var dy0 = y - (ysb + squishOffset);

            var xins = xs - xsb;
            var yins = ys - ysb;

            var inSum = xins + yins;

            var hash =
               (int)(xins - yins + 1) |
               (int)(inSum) << 1 |
               (int)(inSum + yins) << 2 |
               (int)(inSum + xins) << 4;

            var c = lookup2D[hash];

            var value = 0.0;
            while (c != null) {
                var dx = dx0 + c.dx;
                var dy = dy0 + c.dy;
                var attn = 2 - dx * dx - dy * dy;
                if (attn > 0) {
                    var px = xsb + c.xsb;
                    var py = ysb + c.ysb;

                    var i = perm2D[(perm[px & 0xFF] + py) & 0xFF];
                    var valuePart = gradients2D[i] * dx + gradients2D[i + 1] * dy;

                    attn *= attn;
                    value += attn * attn * valuePart;
                }
                c = c.Next;
            }

            return value * NORM_2D;
        }

        public double Wave3D(double x, double y, double z) {
            var stretchOffset = (x + y + z) * STRETCH_3D;
            var xs = x + stretchOffset;
            var ys = y + stretchOffset;
            var zs = z + stretchOffset;

            var xsb = FastFloor(xs);
            var ysb = FastFloor(ys);
            var zsb = FastFloor(zs);

            var squishOffset = (xsb + ysb + zsb) * SQUISH_3D;
            var dx0 = x - (xsb + squishOffset);
            var dy0 = y - (ysb + squishOffset);
            var dz0 = z - (zsb + squishOffset);

            var xins = xs - xsb;
            var yins = ys - ysb;
            var zins = zs - zsb;

            var inSum = xins + yins + zins;

            var hash =
               (int)(yins - zins + 1) |
               (int)(xins - yins + 1) << 1 |
               (int)(xins - zins + 1) << 2 |
               (int)inSum << 3 |
               (int)(inSum + zins) << 5 |
               (int)(inSum + yins) << 7 |
               (int)(inSum + xins) << 9;

            var c = lookup3D[hash];

            var value = 0.0;
            while (c != null) {
                var dx = dx0 + c.dx;
                var dy = dy0 + c.dy;
                var dz = dz0 + c.dz;
                var attn = 2 - dx * dx - dy * dy - dz * dz;
                if (attn > 0) {
                    var px = xsb + c.xsb;
                    var py = ysb + c.ysb;
                    var pz = zsb + c.zsb;

                    var i = perm3D[(perm[(perm[px & 0xFF] + py) & 0xFF] + pz) & 0xFF];
                    var valuePart = gradients3D[i] * dx + gradients3D[i + 1] * dy + gradients3D[i + 2] * dz;

                    attn *= attn;
                    value += attn * attn * valuePart;
                }

                c = c.Next;
            }
            return value * NORM_3D;
        }
        
        static Noise() {
            var base2D = new int[][] {
                new int[] { 1, 1, 0, 1, 0, 1, 0, 0, 0 },
                new int[] { 1, 1, 0, 1, 0, 1, 2, 1, 1 }
            };
            var p2D = new int[] { 0, 0, 1, -1, 0, 0, -1, 1, 0, 2, 1, 1, 1, 2, 2, 0, 1, 2, 0, 2, 1, 0, 0, 0 };
            var lookupPairs2D = new int[] { 0, 1, 1, 0, 4, 1, 17, 0, 20, 2, 21, 2, 22, 5, 23, 5, 26, 4, 39, 3, 42, 4, 43, 3 };

            var contributions2D = new Contribution2[p2D.Length / 4];
            for (int i = 0; i < p2D.Length; i += 4) {
                var baseSet = base2D[p2D[i]];
                Contribution2 previous = null, current = null;
                for (int k = 0; k < baseSet.Length; k += 3) {
                    current = new Contribution2(baseSet[k], baseSet[k + 1], baseSet[k + 2]);
                    if (previous == null) {
                        contributions2D[i / 4] = current;
                    } else {
                        previous.Next = current;
                    }
                    previous = current;
                }
                current.Next = new Contribution2(p2D[i + 1], p2D[i + 2], p2D[i + 3]);
            }

            lookup2D = new Contribution2[64];
            for (var i = 0; i < lookupPairs2D.Length; i += 2) {
                lookup2D[lookupPairs2D[i]] = contributions2D[lookupPairs2D[i + 1]];
            }

            
            var base3D = new int[][] {
                new int[] { 0, 0, 0, 0, 1, 1, 0, 0, 1, 0, 1, 0, 1, 0, 0, 1 },
                new int[] { 2, 1, 1, 0, 2, 1, 0, 1, 2, 0, 1, 1, 3, 1, 1, 1 },
                new int[] { 1, 1, 0, 0, 1, 0, 1, 0, 1, 0, 0, 1, 2, 1, 1, 0, 2, 1, 0, 1, 2, 0, 1, 1 }
            };
            var p3D = new int[] { 0, 0, 1, -1, 0, 0, 1, 0, -1, 0, 0, -1, 1, 0, 0, 0, 1, -1, 0, 0, -1, 0, 1, 0, 0, -1, 1, 0, 2, 1, 1, 0, 1, 1, 1, -1, 0, 2, 1, 0, 1, 1, 1, -1, 1, 0, 2, 0, 1, 1, 1, -1, 1, 1, 1, 3, 2, 1, 0, 3, 1, 2, 0, 1, 3, 2, 0, 1, 3, 1, 0, 2, 1, 3, 0, 2, 1, 3, 0, 1, 2, 1, 1, 1, 0, 0, 2, 2, 0, 0, 1, 1, 0, 1, 0, 2, 0, 2, 0, 1, 1, 0, 0, 1, 2, 0, 0, 2, 2, 0, 0, 0, 0, 1, 1, -1, 1, 2, 0, 0, 0, 0, 1, -1, 1, 1, 2, 0, 0, 0, 0, 1, 1, 1, -1, 2, 3, 1, 1, 1, 2, 0, 0, 2, 2, 3, 1, 1, 1, 2, 2, 0, 0, 2, 3, 1, 1, 1, 2, 0, 2, 0, 2, 1, 1, -1, 1, 2, 0, 0, 2, 2, 1, 1, -1, 1, 2, 2, 0, 0, 2, 1, -1, 1, 1, 2, 0, 0, 2, 2, 1, -1, 1, 1, 2, 0, 2, 0, 2, 1, 1, 1, -1, 2, 2, 0, 0, 2, 1, 1, 1, -1, 2, 0, 2, 0 };
            var lookupPairs3D = new int[] { 0, 2, 1, 1, 2, 2, 5, 1, 6, 0, 7, 0, 32, 2, 34, 2, 129, 1, 133, 1, 160, 5, 161, 5, 518, 0, 519, 0, 546, 4, 550, 4, 645, 3, 647, 3, 672, 5, 673, 5, 674, 4, 677, 3, 678, 4, 679, 3, 680, 13, 681, 13, 682, 12, 685, 14, 686, 12, 687, 14, 712, 20, 714, 18, 809, 21, 813, 23, 840, 20, 841, 21, 1198, 19, 1199, 22, 1226, 18, 1230, 19, 1325, 23, 1327, 22, 1352, 15, 1353, 17, 1354, 15, 1357, 17, 1358, 16, 1359, 16, 1360, 11, 1361, 10, 1362, 11, 1365, 10, 1366, 9, 1367, 9, 1392, 11, 1394, 11, 1489, 10, 1493, 10, 1520, 8, 1521, 8, 1878, 9, 1879, 9, 1906, 7, 1910, 7, 2005, 6, 2007, 6, 2032, 8, 2033, 8, 2034, 7, 2037, 6, 2038, 7, 2039, 6 };

            var contributions3D = new Contribution3[p3D.Length / 9];
            for (int i = 0; i < p3D.Length; i += 9) {
                var baseSet = base3D[p3D[i]];
                Contribution3 previous = null, current = null;
                for (int k = 0; k < baseSet.Length; k += 4) {
                    current = new Contribution3(baseSet[k], baseSet[k + 1], baseSet[k + 2], baseSet[k + 3]);
                    if (previous == null) {
                        contributions3D[i / 9] = current;
                    } else {
                        previous.Next = current;
                    }
                    previous = current;
                }
                current.Next = new Contribution3(p3D[i + 1], p3D[i + 2], p3D[i + 3], p3D[i + 4]);
                current.Next.Next = new Contribution3(p3D[i + 5], p3D[i + 6], p3D[i + 7], p3D[i + 8]);
            }
            
            lookup3D = new Contribution3[2048];
            for (var i = 0; i < lookupPairs3D.Length; i += 2) {
                lookup3D[lookupPairs3D[i]] = contributions3D[lookupPairs3D[i + 1]];
            } 
        }

        // <Open Simplex>
        
        private const double STRETCH_2D = -0.211324865405187;    //(1/Math.sqrt(2+1)-1)/2;
        private const double STRETCH_3D = -1.0 / 6.0;            //(1/Math.sqrt(3+1)-1)/3;
        private const double SQUISH_2D = 0.366025403784439;      //(Math.sqrt(2+1)-1)/2;
        private const double SQUISH_3D = 1.0 / 3.0;              //(Math.sqrt(3+1)-1)/3;
        private const double NORM_2D = 1.0 / 47.0 * 1.1545;
        private const double NORM_3D = 1.0 / 103.0 * 1.05;

        private static double[] gradients2D = {
             5,  2,    2,  5,
            -5,  2,   -2,  5,
             5, -2,    2, -5,
            -5, -2,   -2, -5,
        };

        private static double[] gradients3D = {
            -11,  4,  4,     -4,  11,  4,    -4,  4,  11,
             11,  4,  4,      4,  11,  4,     4,  4,  11,
            -11, -4,  4,     -4, -11,  4,    -4, -4,  11,
             11, -4,  4,      4, -11,  4,     4, -4,  11,
            -11,  4, -4,     -4,  11, -4,    -4,  4, -11,
             11,  4, -4,      4,  11, -4,     4,  4, -11,
            -11, -4, -4,     -4, -11, -4,    -4, -4, -11,
             11, -4, -4,      4, -11, -4,     4, -4, -11,
        };

        private static Contribution2[] lookup2D;
        private static Contribution3[] lookup3D;

        private static int ShuffleEntry(int entry) {
            const uint m0 = 0b10101010101010101010101010101010;
            const uint m1 = 0b01010101010101010101010101010101;
            long p0 = entry & m0;
            long p1 = entry & m1;
            return ((
                (BitReverseTable[(byte) ((p0 & 0xFF) >> 0)] << 24) |
                (BitReverseTable[(byte) ((p0 & 0xFF00) >> 8)] << 16) |
                (BitReverseTable[(byte) ((p0 & 0xFF0000) >> 16)] << 8) |
                (BitReverseTable[(byte) ((p0 & 0xFF000000) >> 24)] << 0)
            ) >> 1) | (int) p1;
        }
    
        public static unsafe int SingleToInt32Bits(float value) {
            return *(int*)(&value);
        }
        
        public static unsafe float Int32BitsToSingle(int value) {
            return *(float*)(&value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FastFloor(double x) {
            var xi = (int)x;
            return x < xi ? xi - 1 : xi;
        }
        
        private static byte[] BitReverseTable = {
            0x00, 0x80, 0x40, 0xc0, 0x20, 0xa0, 0x60, 0xe0,
            0x10, 0x90, 0x50, 0xd0, 0x30, 0xb0, 0x70, 0xf0,
            0x08, 0x88, 0x48, 0xc8, 0x28, 0xa8, 0x68, 0xe8,
            0x18, 0x98, 0x58, 0xd8, 0x38, 0xb8, 0x78, 0xf8,
            0x04, 0x84, 0x44, 0xc4, 0x24, 0xa4, 0x64, 0xe4,
            0x14, 0x94, 0x54, 0xd4, 0x34, 0xb4, 0x74, 0xf4,
            0x0c, 0x8c, 0x4c, 0xcc, 0x2c, 0xac, 0x6c, 0xec,
            0x1c, 0x9c, 0x5c, 0xdc, 0x3c, 0xbc, 0x7c, 0xfc,
            0x02, 0x82, 0x42, 0xc2, 0x22, 0xa2, 0x62, 0xe2,
            0x12, 0x92, 0x52, 0xd2, 0x32, 0xb2, 0x72, 0xf2,
            0x0a, 0x8a, 0x4a, 0xca, 0x2a, 0xaa, 0x6a, 0xea,
            0x1a, 0x9a, 0x5a, 0xda, 0x3a, 0xba, 0x7a, 0xfa,
            0x06, 0x86, 0x46, 0xc6, 0x26, 0xa6, 0x66, 0xe6,
            0x16, 0x96, 0x56, 0xd6, 0x36, 0xb6, 0x76, 0xf6,
            0x0e, 0x8e, 0x4e, 0xce, 0x2e, 0xae, 0x6e, 0xee,
            0x1e, 0x9e, 0x5e, 0xde, 0x3e, 0xbe, 0x7e, 0xfe,
            0x01, 0x81, 0x41, 0xc1, 0x21, 0xa1, 0x61, 0xe1,
            0x11, 0x91, 0x51, 0xd1, 0x31, 0xb1, 0x71, 0xf1,
            0x09, 0x89, 0x49, 0xc9, 0x29, 0xa9, 0x69, 0xe9,
            0x19, 0x99, 0x59, 0xd9, 0x39, 0xb9, 0x79, 0xf9,
            0x05, 0x85, 0x45, 0xc5, 0x25, 0xa5, 0x65, 0xe5,
            0x15, 0x95, 0x55, 0xd5, 0x35, 0xb5, 0x75, 0xf5,
            0x0d, 0x8d, 0x4d, 0xcd, 0x2d, 0xad, 0x6d, 0xed,
            0x1d, 0x9d, 0x5d, 0xdd, 0x3d, 0xbd, 0x7d, 0xfd,
            0x03, 0x83, 0x43, 0xc3, 0x23, 0xa3, 0x63, 0xe3,
            0x13, 0x93, 0x53, 0xd3, 0x33, 0xb3, 0x73, 0xf3,
            0x0b, 0x8b, 0x4b, 0xcb, 0x2b, 0xab, 0x6b, 0xeb,
            0x1b, 0x9b, 0x5b, 0xdb, 0x3b, 0xbb, 0x7b, 0xfb,
            0x07, 0x87, 0x47, 0xc7, 0x27, 0xa7, 0x67, 0xe7,
            0x17, 0x97, 0x57, 0xd7, 0x37, 0xb7, 0x77, 0xf7,
            0x0f, 0x8f, 0x4f, 0xcf, 0x2f, 0xaf, 0x6f, 0xef,
            0x1f, 0x9f, 0x5f, 0xdf, 0x3f, 0xbf, 0x7f, 0xff
        };

        private class Contribution2 {
            public double dx, dy;
            public int xsb, ysb;
            public Contribution2 Next;

            public Contribution2(double multiplier, int xsb, int ysb) {
                dx = -xsb - multiplier * SQUISH_2D;
                dy = -ysb - multiplier * SQUISH_2D;
                this.xsb = xsb;
                this.ysb = ysb;
            }
        }

        private class Contribution3 {
            public double dx, dy, dz;
            public int xsb, ysb, zsb;
            public Contribution3 Next;

            public Contribution3(double multiplier, int xsb, int ysb, int zsb) {
                dx = -xsb - multiplier * SQUISH_3D;
                dy = -ysb - multiplier * SQUISH_3D;
                dz = -zsb - multiplier * SQUISH_3D;
                this.xsb = xsb;
                this.ysb = ysb;
                this.zsb = zsb;
            }
        }
    }
}