using System;
using System.Runtime.CompilerServices;

namespace Game.Data {
    public sealed class ChunkSoil {
        public const int Size1D = 32;
        public const int Size2D = 32 * 32;
        public const int Size3D = 32 * 32 * 32;
        public const int Size4D = 34 * 34 * 34;

        public static readonly float[] CubeDensity = {
            0.00f, 0.10f, 0.20f, 0.30f, 0.40f,
            0.55f, 0.60f, 0.65f, 0.70f, 0.75f,
            0.80f, 0.85f, 0.90f, 0.95f, 1.00f, 2.00f // [Special Case]
        };

        public byte[] density;
        public byte[] material;

        private const int ModeIndex = 0;
        private const int PaletteIndex = 1;

        public readonly struct Mode {
            private const byte ConstantId = 0;
            private const byte Packed1Id = 1;
            private const byte Packed2Id = 2;
            private const byte Packed4Id = 3;
            private const byte Packed6Id = 4;

            public static readonly Mode Constant = new (ConstantId,  0,     0,  1);
            public static readonly Mode Packed1  = new (Packed1Id ,  2,  4096,  4);
            public static readonly Mode Packed2  = new (Packed2Id ,  4,  8192,  6);
            public static readonly Mode Packed4  = new (Packed4Id , 16, 16384, 18);
            public static readonly Mode Packed6  = new (Packed6Id ,  0, 24576,  1);

            private static readonly Mode[] Packs = { Constant, Packed1, Packed2, Packed4, Packed6 };

            public readonly byte id;
            public readonly int paletteCapacity;
            public readonly int dataSize;
            public readonly int header;

            public int MatArraySize => (header + dataSize + 3) & ~3;
            public int DenArraySize => dataSize + 4;

            public Mode(byte id, int paletteCapacity, int dataSize, int header) {
                this.id = id;
                this.paletteCapacity = paletteCapacity;
                this.dataSize = dataSize;
                this.header = header;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Mode Parse(int id) {
                return Packs[id];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int ReadBits(byte[] data, int offset, int index) {
                switch (id) {
                    case ConstantId:
                        return data[offset];
                    case Packed1Id:
                        return ReadBits1(data, offset, index);
                    case Packed2Id:
                        return ReadBits2(data, offset, index);
                    case Packed4Id:
                        return ReadBits4(data, offset, index);
                    case Packed6Id:
                        return ReadBits6(data, offset, index);
                }

                return 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int ReadBits1(byte[] data, int offset, int index) {
                int byteIndex = offset + (index >> 3);
                int shift = index & 7;

                return (data[byteIndex] >> shift) & 1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int ReadBits2(byte[] data, int offset, int index) {
                int bit = index << 1;
                int byteIndex = offset + (bit >> 3);
                int shift = bit & 7;

                return (data[byteIndex] >> shift) & 3;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int ReadBits4(byte[] data, int offset, int index) {
                int bit = index << 2;
                int byteIndex = offset + (bit >> 3);
                int shift = bit & 7;

                return (data[byteIndex] >> shift) & 15;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int ReadBits6(byte[] data, int offset, int index) {
                int bit = index * 6;
                int byteIndex = offset + (bit >> 3);
                int shift = bit & 7;

                uint value = data[byteIndex];

                if (shift > 2)
                    value |= (uint)data[byteIndex + 1] << 8;

                return (int)((value >> shift) & 63);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteBits(byte[] data, int offset, int index, int value) {
                switch (id) {
                    case ConstantId:
                        data[offset] = (byte)value;
                        break;
                    case Packed1Id: 
                        WriteBits1(data, offset, index, value);
                        break;
                    case Packed2Id: 
                        WriteBits2(data, offset, index, value);
                        break;
                    case Packed4Id: 
                        WriteBits4(data, offset, index, value);
                        break;
                    case Packed6Id: 
                        WriteBits6(data, offset, index, value);
                        break;
                }
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteBits1(byte[] data, int offset, int index, int value) {
                int byteIndex = offset + (index >> 3);
                int shift = index & 7;

                byte mask = (byte)(1 << shift);

                if ((value & 1) != 0)
                    data[byteIndex] |= mask;
                else
                    data[byteIndex] &= (byte)~mask;
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteBits2(byte[] data, int offset, int index, int value) {
                int bit = index << 1;
                int byteIndex = offset + (bit >> 3);
                int shift = bit & 7;

                byte mask = (byte)(3 << shift);

                data[byteIndex] = (byte)((data[byteIndex] & ~mask) | ((value & 3) << shift));
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteBits4(byte[] data, int offset, int index, int value) {
                int bit = index << 2;
                int byteIndex = offset + (bit >> 3);
                int shift = bit & 7;

                byte mask = (byte)(15 << shift);

                data[byteIndex] = (byte)((data[byteIndex] & ~mask) | ((value & 15) << shift));
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteBits6(byte[] data, int offset, int index, int value) {
                int bit = index * 6;
                int byteIndex = offset + (bit >> 3);
                int shift = bit & 7;

                uint current = data[byteIndex];

                if (shift > 2)
                    current |= (uint)data[byteIndex + 1] << 8;

                uint mask = 63u << shift;

                current = (current & ~mask) | ((uint)(value & 63) << shift);

                data[byteIndex] = (byte)current;

                if (shift > 2)
                    data[byteIndex + 1] = (byte)(current >> 8);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetAll(byte[] data, int offset, int value) {
                switch (id) {
                    case ConstantId: {
                        data[offset] = (byte)value;
                        return;
                    }
                    case Packed1Id: {
                        byte packed = (value & 1) != 0 ? (byte)0xFF : (byte)0x00;
                        Array.Fill(data, packed, offset, dataSize);
                        return;
                    }
                    case Packed2Id: {
                        byte v = (byte)(value & 3);
                        byte packed = (byte)(v | (v << 2) | (v << 4) | (v << 6));
                        Array.Fill(data, packed, offset, dataSize);
                        return;
                    }
                    case Packed4Id: {
                        byte v = (byte)(value & 15);
                        byte packed = (byte)(v | (v << 4));
                        Array.Fill(data, packed, offset, dataSize);
                        return;
                    }
                    case Packed6Id: {
                        value &= 63;

                        uint pattern =
                            (uint)value |
                            ((uint)value << 6) |
                            ((uint)value << 12) |
                            ((uint)value << 18);

                        byte b0 = (byte)pattern;
                        byte b1 = (byte)(pattern >> 8);
                        byte b2 = (byte)(pattern >> 16);

                        int end = offset + dataSize;

                        for (int i = offset; i < end; i += 3) {
                            data[i] = b0;
                            data[i + 1] = b1;
                            data[i + 2] = b2;
                        }

                        break;
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int FindCompactPalette(byte[] data, ref Span<byte> palette) {
                if (id == ConstantId) return -1;
                
                ulong used = 0;
                int countLimit = Parse(id - 1).paletteCapacity;
                int count = 0;

                if (id == Packed1Id) {
                    byte val = data[header];
                    byte rep = (byte)((val & 0x1) == 0x1 ? 0xF : 0x0);
                    
                    count = 1;
                    palette[0] = data[2 + (val & 0x1)];
                    for (int i = 0, len = Size3D / 8; i < len; i++) {
                        if (data[header + i] != rep) return -1;
                    }

                } else if (id == Packed2Id) {
                    for (int i = 0; i < Size3D; i++) {
                        int value = data[2 + ReadBits2(data, header, i)];
                        ulong flag = 1UL << value;
                        if ((used & flag) == 0) {
                            if (count == countLimit) return -1;
                            palette[count++] = (byte)value;
                        }

                        used |= flag;
                    }
                    
                } else if (id == Packed4Id) {
                    for (int i = 0; i < Size3D; i++) {
                        int value = data[2 + ReadBits4(data, header, i)];
                        ulong flag = 1UL << value;
                        if ((used & flag) == 0) {
                            if (count == countLimit) return -1;
                            palette[count++] = (byte)value;
                        }

                        used |= flag;
                    }
                    
                } else if (id == Packed6Id) {
                    for (int i = 0; i < Size3D; i++) {
                        int value = ReadBits6(data, header, i);
                        ulong flag = 1UL << value;
                        if ((used & flag) == 0) {
                            if (count == countLimit) return -1;
                            palette[count++] = (byte)value;
                        }

                        used |= flag;
                    }
                    
                }
                return count;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int GetDensityValue(byte[] data, int index) {
                if (id == ConstantId) return data[1];

                return (data[1 + (index >> 1)] >> ((index & 1) << 2)) & 15;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int GetMaterialValue(byte[] data, int index) {
                if (id == ConstantId) return data[1];
                if (id == Packed6Id) return ReadBits6(data, 1, index);

                return data[2 + ReadBits(data, 2 + paletteCapacity, index)];
            }

            public static bool operator ==(Mode left, Mode right) => left.id == right.id;
            public static bool operator !=(Mode left, Mode right) => left.id != right.id;
            public override bool Equals(object obj) => obj is Mode other && id == other.id;
            public override int GetHashCode() => id.GetHashCode();
            public override string ToString() {
                return id switch {
                    Packed1Id => "Packed1",
                    Packed2Id => "Packed2",
                    Packed4Id => "Packed4",
                    Packed6Id => "Packed6",
                    _ => "Constant"
                };
            }
        }

        public ChunkSoil() {
            density = CreateDensity(Mode.Constant);
            material = CreateMaterial(Mode.Constant);
        }

        public ChunkSoil(byte[] density, byte[] material) {
            this.density = density ?? throw new ArgumentNullException(nameof(density));
            this.material = material ?? throw new ArgumentNullException(nameof(material));
            Validate();
        }

        public Mode DensityBitSize => Mode.Parse(density[ModeIndex]);
        public Mode MaterialBitSize => Mode.Parse(material[ModeIndex]);

        public float GetDensity(int x, int y, int z) {
            var currentMode = Mode.Parse(density[ModeIndex]);
            
            return CubeDensity[currentMode.GetDensityValue(density, Index(x, y, z))];
        }

        public float UnsafeGetDensity(int x, int y, int z) {
            var currentMode = Mode.Parse(density[ModeIndex]);
            
            return CubeDensity[currentMode.GetDensityValue(density, UnsafeIndex(x, y, z))];
        }

        public void SetDensity(int x, int y, int z, float value) {
            SetDensityValue(Index(x, y, z), QuantizeDensity(value));
        }

        public int GetMaterial(int x, int y, int z) {
            var currentMode = Mode.Parse(material[ModeIndex]);
            
            return currentMode.GetMaterialValue(material, Index(x, y, z));
        }

        public void SetMaterial(int x, int y, int z, int value) {
            if ((uint)value > 63)
                throw new ArgumentOutOfRangeException(nameof(value));

            SetMaterialValue(Index(x, y, z), value);
        }

        public void ExpandDensity() {
            var currentMode = Mode.Parse(density[ModeIndex]);
            
            if (currentMode == Mode.Packed4) return;

            byte[] source = density;
            byte[] target = CreateDensity(Mode.Packed4);

            var targetMode = Mode.Packed4;
            
            int value = currentMode.GetDensityValue(source, 0);
            if (value != 0) {
                targetMode.SetAll(target, 1, value);
            }

            density = target;
        }

        private static byte[] CreateDensity(Mode mode) {
            byte[] result = new byte[mode.DenArraySize];
            result[0] = mode.id;
            return result;
        }

        public void ExpandMaterial() {
            var currentMode = Mode.Parse(material[ModeIndex]);
            
            if (currentMode == Mode.Packed6) return;

            ExpandMaterial(Mode.Parse(currentMode.id + 1));
        }

        public void ExpandMaterial(int paletteCount) {
            var currentMode = Mode.Parse(material[ModeIndex]);
            
            if (currentMode == Mode.Packed6 || paletteCount <= currentMode.paletteCapacity) return;

            var targetMode = paletteCount <= 2 ? Mode.Packed1 : 
                             paletteCount <= 4 ? Mode.Packed2 : 
                             paletteCount <= 16 ? Mode.Packed4 : Mode.Packed6;
            
            ExpandMaterial(targetMode);
        }

        public int PaletteCount {
            get {
                var currentMode = Mode.Parse(material[ModeIndex]);
                if (currentMode == Mode.Constant) return 1;
                if (currentMode == Mode.Packed6) return 64;
                return material[PaletteIndex];
            }
        }

        private void ExpandMaterial(Mode targetMode) {
            var currentMode = Mode.Parse(material[ModeIndex]);
            
            byte[] source = material;
            byte[] target = CreateMaterial(targetMode);
            
            if (targetMode != Mode.Packed6) {
                if (currentMode == Mode.Constant) {
                    target[PaletteIndex] = 1;
                    target[PaletteIndex + 1] = source[PaletteIndex];
                } else {
                    int count = source[PaletteIndex];
                    Array.Copy(source, PaletteIndex, target, PaletteIndex, count + 1);
                }
            }

            int headerSize = targetMode.header;
            int cHeaderSize = currentMode.header;
            if (currentMode == Mode.Constant) {
                if (targetMode == Mode.Packed6) {
                    int value = currentMode.GetMaterialValue(source, 0);
                    if (value != 0) {
                        targetMode.SetAll(target, headerSize, value);
                    }
                }
            } else {
                if (currentMode == Mode.Packed1 && targetMode == Mode.Packed2) {
                    ExpandMaterialPacked1To2(source, target, currentMode, targetMode);
                    
                } else if (currentMode == Mode.Packed1 && targetMode == Mode.Packed4) {
                    ExpandMaterialPacked1To4(source, target, currentMode, targetMode);
                    
                } else if (currentMode == Mode.Packed1 && targetMode == Mode.Packed6) {
                    for (int i = 0; i < Size3D; i++) {
                        int value = currentMode.ReadBits1(source, cHeaderSize, i);
                        targetMode.WriteBits6(target, headerSize, i, source[PaletteIndex + 1 + value]);
                    }
                    
                } else if (currentMode == Mode.Packed2 && targetMode == Mode.Packed4) {
                    ExpandMaterialPacked2To4(source, target, currentMode, targetMode);
                    
                } else if (currentMode == Mode.Packed2 && targetMode == Mode.Packed6) {
                    for (int i = 0; i < Size3D; i++) {
                        int value = currentMode.ReadBits2(source, cHeaderSize, i);
                        targetMode.WriteBits6(target, headerSize, i, source[PaletteIndex + 1 + value]);
                    }
                    
                } else if (currentMode == Mode.Packed4 && targetMode == Mode.Packed6) {
                    for (int i = 0; i < Size3D; i++) {
                        int value = currentMode.ReadBits4(source, cHeaderSize, i);
                        targetMode.WriteBits6(target, headerSize, i, source[PaletteIndex + 1 + value]);
                    }
                    
                }
            }

            material = target;
        }

        public byte[] ExportDensity() {
            var compacted = LocalCompactDensity();
            if (compacted != null) {
                return compacted;
            }

            return (byte[])density.Clone();
        }

        public void CompactDensity() {
            var compacted = LocalCompactDensity();
            if (compacted != null) {
                density = compacted;
            }
        }

        private byte[] LocalCompactDensity() {
            byte[] source = density;
            var currentMode = Mode.Parse(source[ModeIndex]);
            
            if (currentMode == Mode.Constant) return null;
            
            int first = currentMode.GetDensityValue(source, 0);
            bool constant = true;

            for (int i = 1; i < Size3D; i++) {
                if (currentMode.GetDensityValue(source, i) != first) {
                    constant = false;
                    break;
                }
            }

            if (constant) {
                source = CreateDensity(Mode.Constant);
                source[1] = (byte)first;
                return source;
            }
            return null;
        }

        public byte[] ExportMaterial() {
            var compacted = LocalCompactMaterial();
            if (compacted != null) {
                return compacted;
            }

            return (byte[])material.Clone();
        }

        public void CompactMaterial() {
            var compacted = LocalCompactMaterial();
            if (compacted != null) {
                material = compacted;
            }
        }

        private byte[] LocalCompactMaterial() {
            byte[] source = material;
            var currentMode = Mode.Parse(source[ModeIndex]);
            
            Span<byte> palette = stackalloc byte[16];
            int count = currentMode.FindCompactPalette(source, ref palette);
            if (count == -1) {
                return null;
            }

            var targetMode = count <= 1 ? Mode.Constant : 
                             count <= 2 ? Mode.Packed1 : 
                             count <= 4 ? Mode.Packed2 : Mode.Packed4;

            if (targetMode.id >= currentMode.id) return null;

            if (count == 1) {
                var mat = CreateMaterial(Mode.Constant);
                mat[1] = palette[0];
                return mat;
            }
            
            var target = CreateMaterial(targetMode);
                
            target[PaletteIndex] = (byte)count;
            for (int i = 0; i < count; i++) {
                target[PaletteIndex + 1 + i] = palette[i];
            }
            
            Span<int> paletteLookup = stackalloc int[64];
            for (int i = 0; i < count; i++) {
                paletteLookup[palette[i]] = i;
            }
            
            int headerSize = targetMode.header;
            int cHeaderSize = currentMode.header;
            if (currentMode == Mode.Packed2 && targetMode == Mode.Packed1) {
                for (int i = 0; i < Size3D; i++) {
                    int value = source[PaletteIndex + 1 + currentMode.ReadBits2(source, cHeaderSize, i)];
                    targetMode.WriteBits1(target, headerSize, i, paletteLookup[value]);
                }
            } else if (currentMode == Mode.Packed4 && targetMode == Mode.Packed1) {
                for (int i = 0; i < Size3D; i++) {
                    int value = source[PaletteIndex + 1 + currentMode.ReadBits4(source, cHeaderSize, i)];
                    targetMode.WriteBits1(target, headerSize, i, paletteLookup[value]);
                }
            } else if (currentMode == Mode.Packed4 && targetMode == Mode.Packed2) {
                for (int i = 0; i < Size3D; i++) {
                    int value = source[PaletteIndex + 1 + currentMode.ReadBits4(source, cHeaderSize, i)];
                    targetMode.WriteBits2(target, headerSize, i, paletteLookup[value]);
                }
            } else if (currentMode == Mode.Packed6 && targetMode == Mode.Packed1) {
                for (int i = 0; i < Size3D; i++) {
                    int value = currentMode.ReadBits6(source, cHeaderSize, i);
                    targetMode.WriteBits1(target, headerSize, i, paletteLookup[value]);
                }
            } else if (currentMode == Mode.Packed6 && targetMode == Mode.Packed2) {
                for (int i = 0; i < Size3D; i++) {
                    int value = currentMode.ReadBits6(source, cHeaderSize, i);
                    targetMode.WriteBits2(target, headerSize, i, paletteLookup[value]);
                }
            } else if (currentMode == Mode.Packed6 && targetMode == Mode.Packed4) {
                for (int i = 0; i < Size3D; i++) {
                    int value = currentMode.ReadBits6(source, cHeaderSize, i);
                    targetMode.WriteBits4(target, headerSize, i, paletteLookup[value]);
                }
            }

            return target;
        }

        private void SetDensityValue(int index, int value) {
            var mode = Mode.Parse(density[ModeIndex]);

            if (mode == Mode.Constant) {
                if (density[1] == value) return;
                ExpandDensity();
                mode = Mode.Packed4;
            }

            mode.WriteBits(density, 1, index, value);
        }

        private void SetMaterialValue(int index, int value) {
            var mode = Mode.Parse(material[ModeIndex]);

            if (mode == Mode.Constant) {
                if (material[1] == value) return;

                ExpandMaterial();
                mode = Mode.Packed1;
            }

            if (mode == Mode.Packed6) {
                mode.WriteBits(material, 1, index, value);
                return;
            }

            int count = material[PaletteIndex];
            int paletteIndex = FindPaletteIndex(material, value);

            if (paletteIndex >= 0) {
                mode.WriteBits(material, mode.header, index, paletteIndex);
                return;
            }

            if (count < mode.paletteCapacity) {
                paletteIndex = AddMaterialToPalette(value);
                mode.WriteBits(material, mode.header, index, paletteIndex);
                return;
            }

            ExpandMaterial();
            SetMaterialValue(index, value);
        }

        private int AddMaterialToPalette(int value) {
            int count = material[PaletteIndex];

            material[PaletteIndex + 1 + count] = (byte)value;
            material[PaletteIndex] = (byte)(count + 1);
            return count;
        }
        
        private static byte[] CreateMaterial(Mode mode) {
            byte[] result = new byte[mode.MatArraySize];
            result[ModeIndex] = mode.id;
            return result;
        }

        private static int FindPaletteIndex(byte[] data, int value) {
            int count = data[PaletteIndex];

            for (int i = 0; i < count; i++) {
                if (data[2 + i] == value)
                    return i;
            }

            return -1;
        }

        private static int UnsafeIndex(int x, int y, int z) {
            return x + (z << 5) + (y << 10);
        }

        private static int Index(int x, int y, int z) {
            if ((uint)x >= Size1D || (uint)y >= Size1D || (uint)z >= Size1D)
                throw new ArgumentOutOfRangeException();

            return x + (z << 5) + (y << 10);
        }
        
        private static int QuantizeDensity(float value) {
            if (float.IsNaN(value) || value <= 0.0f)
                return 0;
            
            if (value < 0.5f)
                return Math.Min(4, (int)MathF.Round(value * 10.0f));

            if (value <= 1.0f)
                return 5 + (int)MathF.Round((value - 0.55f) * 20.0f);

            return 15;
        }

        private void Validate() {
            if (density.Length < 4 || material.Length < 4)
                throw new ArgumentException("Invalid chunk data.");
            
            if (density[ModeIndex] >= 5 || material[ModeIndex] >= 5)
                throw new ArgumentException("Invalid chunk data.");
            
            var mDen = Mode.Parse(density[ModeIndex]);
            var mMat = Mode.Parse(material[ModeIndex]);

            if (mDen != Mode.Constant && mDen != Mode.Packed4) {
                throw new ArgumentException("Invalid chunk data.");
            } 
            
            if (mMat != Mode.Constant && mMat != Mode.Packed6 && material[PaletteIndex] > mMat.paletteCapacity) {
                throw new ArgumentException("Invalid chunk data.");
            }
            
            if (density.Length != mDen.DenArraySize || material.Length != mMat.MatArraySize) {
                throw new ArgumentException("Invalid chunk data.");
            }
        }
        
        private static void ExpandMaterialPacked1To2(byte[] src, byte[] dst, Mode srcMode, Mode dstMode) {
            int srcOffset = srcMode.header;
            int dstOffset = dstMode.header;
            
            int size = Size3D / 8;
            for (int i = 0; i < size; i++) {
                byte value = src[srcOffset + i];

                dst[dstOffset++] = (byte)((value & 0b00000001) |
                                          (value & 0b00000010) << 1 |
                                          (value & 0b00000100) << 2 |
                                          (value & 0b00001000) << 3);

                dst[dstOffset++] = (byte)((value & 0b00010000) >> 4 |
                                          (value & 0b00100000) >> 3 |
                                          (value & 0b01000000) >> 2 |
                                          (value & 0b10000000) >> 1);
            }
        }
        
        private static void ExpandMaterialPacked2To4(byte[] src, byte[] dst, Mode srcMode, Mode dstMode) {
            int srcOffset = srcMode.header;
            int dstOffset = dstMode.header;
            
            int size = Size3D / 4;
            for (int i = 0; i < size; i++) {
                byte value = src[srcOffset + i];

                dst[dstOffset++] = (byte)((value & 0b00000011) |
                                          (value & 0b00001100) << 2);

                dst[dstOffset++] = (byte)((value & 0b00110000) >> 4 |
                                          (value & 0b11000000) >> 2);
            }
        }
        
        private static void ExpandMaterialPacked1To4(byte[] src, byte[] dst, Mode srcMode, Mode dstMode) {
            int srcOffset = srcMode.header;
            int dstOffset = dstMode.header;
            
            int size = Size3D / 8;
            for (int i = 0; i < size; i++) {
                byte value = src[srcOffset + i];

                dst[dstOffset++] = (byte)((value & 0b00000001) |
                                          (value & 0b00000010) << 3);

                dst[dstOffset++] = (byte)((value & 0b00000100) >> 2 |
                                          (value & 0b00001000) << 1);
                
                dst[dstOffset++] = (byte)((value & 0b00010000) >> 4 |
                                          (value & 0b00100000) >> 1);
                
                dst[dstOffset++] = (byte)((value & 0b01000000) >> 6 |
                                          (value & 0b10000000) >> 3);
            }
        }

        public bool IsEmpty() {
            if (density.Length == 4) {
                return GetDensity(0, 0, 0) == 0.0f;
            }

            return false;
        }
        
        public static ChunkSoil CreateFromRaw(ReadOnlySpan<byte> rawMaterial, ReadOnlySpan<byte> rawDensity, ReadOnlySpan<byte> palette, int count) {
            var chunk = new ChunkSoil();

            byte firstDensity = rawDensity[0];
            bool isDensityConstant = true;

            for (int i = 1; i < Size3D; i++) {
                if (rawDensity[i] != firstDensity) {
                    isDensityConstant = false;
                    break;
                }
            }

            if (isDensityConstant) {
                chunk.density = CreateDensity(Mode.Constant);
                chunk.density[1] = firstDensity;
            } else {
                chunk.density = CreateDensity(Mode.Packed4);
                var packed4 = Mode.Packed4;
                for (int i = 0; i < Size3D; i++) {
                    packed4.WriteBits4(chunk.density, 1, i, rawDensity[i]);
                }
            }
            
            if (count <= 1) {
                chunk.material = CreateMaterial(Mode.Constant);
                chunk.material[1] = rawMaterial[0];
                return chunk;
            }

            Mode targetMode = count <= 2 ? Mode.Packed1 :
                              count <= 4 ? Mode.Packed2 :
                              count <= 16 ? Mode.Packed4 : Mode.Packed6;

            chunk.material = CreateMaterial(targetMode);

            if (targetMode == Mode.Packed6) {
                for (int i = 0; i < Size3D; i++) {
                    targetMode.WriteBits6(chunk.material, 1, i, rawMaterial[i]);
                }
            } else {
                Span<int> lookup = stackalloc int[64];
                for (int i = 0; i < count; i++) {
                    lookup[palette[i]] = i;
                }

                chunk.material[PaletteIndex] = (byte)count;
                for (int i = 0; i < count; i++) {
                    chunk.material[PaletteIndex + 1 + i] = palette[i];
                }

                int headerSize = targetMode.header;
                if (targetMode == Mode.Packed4) {
                    for (int i = 0; i < Size3D; i++) {
                        int paletteIdx = lookup[rawMaterial[i]];
                        targetMode.WriteBits4(chunk.material, headerSize, i, paletteIdx);
                    }
                } else if (targetMode == Mode.Packed2) {
                    for (int i = 0; i < Size3D; i++) {
                        int paletteIdx = lookup[rawMaterial[i]];
                        targetMode.WriteBits2(chunk.material, headerSize, i, paletteIdx);
                    }
                } else {
                    for (int i = 0; i < Size3D; i++) {
                        int paletteIdx = lookup[rawMaterial[i]];
                        targetMode.WriteBits1(chunk.material, headerSize, i, paletteIdx);
                    }
                }
            }

            return chunk;
        }

        [ThreadStatic] private static byte[] RawCacheMaterial;
        [ThreadStatic] private static byte[] RawCacheDensity;
        [ThreadStatic] private static byte[] SupRawCacheMaterial;
        [ThreadStatic] private static byte[] SupRawCacheDensity;
        
        public static ChunkSoil DownsampleFromLod0(ChunkSoil[] lod0SubSet, int lodStep) {
            if (RawCacheMaterial == null) {
                RawCacheMaterial = new byte[Size3D];
                RawCacheDensity = new byte[Size3D];
                SupRawCacheMaterial = new byte[Size3D];
                SupRawCacheDensity = new byte[Size3D];
            }
            
            var rawMaterial = RawCacheMaterial;
            var rawDensity = RawCacheDensity;

            Span<byte> palette = stackalloc byte[64];
            Span<bool> matSeen = stackalloc bool[64];
            int uniqueMaterials = 0;

            int samplesPerAxis = Size1D / lodStep; // LOD1: 16 | LOD2: 8 | LOD3: 4

            Span<byte> chunkSampleMat = SupRawCacheMaterial;
            Span<byte> chunkSampleDen = SupRawCacheDensity;

            int chunkIdx = 0;
            for (int cy = 0; cy < lodStep; cy++)
            for (int cz = 0; cz < lodStep; cz++)
            for (int cx = 0; cx < lodStep; cx++, chunkIdx++) {

                var soil = lod0SubSet[chunkIdx];

                UnpackMaterialSparseBulk(soil.material, chunkSampleMat, lodStep);
                UnpackDensitySparseBulk(soil.density, chunkSampleDen, lodStep);

                int startX = cx * samplesPerAxis;
                int startY = cy * samplesPerAxis;
                int startZ = cz * samplesPerAxis;

                int srcReadIdx = 0;
                for (int ly = 0; ly < samplesPerAxis; ly++) {
                    int dstY = (startY + ly) << 10;

                    for (int lz = 0; lz < samplesPerAxis; lz++) {
                        int dstZY = (startZ + lz << 5) + dstY;

                        for (int lx = 0; lx < samplesPerAxis; lx++, srcReadIdx++) {
                            int dstIndex = (startX + lx) + dstZY;

                            byte mat = chunkSampleMat[srcReadIdx];
                            byte rawDenVal = chunkSampleDen[srcReadIdx];

                            float denFloat = CubeDensity[rawDenVal];
                            denFloat = Math.Clamp(0.5f + (denFloat - 0.5f) * 2.0f, 0.0f, 1.0f);

                            rawDensity[dstIndex] = (byte)QuantizeDensity(denFloat);
                            rawMaterial[dstIndex] = mat;

                            if (!matSeen[mat]) {
                                matSeen[mat] = true;
                                palette[uniqueMaterials] = mat;
                                uniqueMaterials++;
                            }
                        }
                    }
                }
            }

            return CreateFromRaw(rawMaterial, rawDensity, palette, uniqueMaterials);
        }

        private static void UnpackMaterialSparseBulk(byte[] src, Span<byte> dst, int step) {
            var mode = Mode.Parse(src[ModeIndex]);
            int writeIdx = 0;

            if (mode == Mode.Constant) {
                dst.Fill(src[1]);
                return;
            }

            if (mode == Mode.Packed6) {
                for (int y = 0; y < 32; y += step) {
                    int yOffset = y << 10;
                    for (int z = 0; z < 32; z += step) {
                        int zyOffset = (z << 5) + yOffset;
                        for (int x = 0; x < 32; x += step, writeIdx++) {
                            int srcIdx = x + zyOffset;
                            dst[writeIdx] = (byte)mode.ReadBits6(src, 1, srcIdx);
                        }
                    }
                }
                return;
            }

            int header = mode.header;

            if (mode == Mode.Packed4) {
                for (int y = 0; y < 32; y += step) {
                    int yOffset = y << 10;
                    for (int z = 0; z < 32; z += step) {
                        int zyOffset = (z << 5) + yOffset;
                        for (int x = 0; x < 32; x += step, writeIdx++) {
                            int srcIdx = x + zyOffset;
                            int paletteIdx = mode.ReadBits4(src, header, srcIdx);
                            dst[writeIdx] = src[PaletteIndex + 1 + paletteIdx];
                        }
                    }
                }
            } else if (mode == Mode.Packed2) {
                for (int y = 0; y < 32; y += step) {
                    int yOffset = y << 10;
                    for (int z = 0; z < 32; z += step) {
                        int zyOffset = (z << 5) + yOffset;
                        for (int x = 0; x < 32; x += step, writeIdx++) {
                            int srcIdx = x + zyOffset;
                            int paletteIdx = mode.ReadBits2(src, header, srcIdx);
                            dst[writeIdx] = src[PaletteIndex + 1 + paletteIdx];
                        }
                    }
                }
            } else {
                for (int y = 0; y < 32; y += step) {
                    int yOffset = y << 10;
                    for (int z = 0; z < 32; z += step) {
                        int zyOffset = (z << 5) + yOffset;
                        for (int x = 0; x < 32; x += step, writeIdx++) {
                            int srcIdx = x + zyOffset;
                            int paletteIdx = mode.ReadBits1(src, header, srcIdx);
                            dst[writeIdx] = src[PaletteIndex + 1 + paletteIdx];
                        }
                    }
                }
            }
        }

        private static void UnpackDensitySparseBulk(byte[] src, Span<byte> dst, int step) {
            var mode = Mode.Parse(src[ModeIndex]);

            if (mode == Mode.Constant) {
                dst.Fill(src[1]);
                return;
            }

            int writeIdx = 0;
            for (int y = 0; y < 32; y += step) {
                int yOffset = y << 10;
                for (int z = 0; z < 32; z += step) {
                    int zyOffset = (z << 5) + yOffset;
                    for (int x = 0; x < 32; x += step, writeIdx++) {
                        int srcIdx = x + zyOffset;
                        dst[writeIdx] = (byte)((src[1 + (srcIdx >> 1)] >> ((srcIdx & 1) << 2)) & 15);
                    }
                }
            }
        }
    }
}