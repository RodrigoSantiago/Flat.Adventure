using System.Runtime.InteropServices;
using UnityEngine;

namespace Adventure.Logic.Data {
    
    [StructLayout(LayoutKind.Sequential)]
    public struct Voxel {
        
        private static float[] byteToFloat = {
            0.000f,
            0.067f,
            0.133f,
            0.200f,
            0.267f,
            0.333f,
            0.400f,
            0.467f,
            0.533f,
            0.600f,
            0.667f,
            0.733f,
            0.800f,
            0.867f,
            0.933f,
            1.000f
        };
        
        public static float ToFloat(byte b) {
            return b / 15f;//b / 15f;
        }

        public static byte ToByte(float f) {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(f) * 15);//(byte)Mathf.RoundToInt(Mathf.Clamp01(f) * 15);
        }
        
        public byte material;
        public byte volume;

        public int Material {
            get {
                return material;
            }
        }

        public float Volume {
            get {
                return ToFloat(volume);
            }
        }

        public ushort Pack {
            get {
                return (ushort)((volume << 8) | material);
            }
        }

        public Voxel(byte material, byte volume) {
            this.material = material;
            this.volume = volume;
        }

        public Voxel(int material, float volume) {
            this.material = (byte)material;
            this.volume = ToByte(volume);
        }

        public Voxel(int packData) {
            this.material = (byte)(packData & 0xFF);
            this.volume = (byte)((packData & 0xFF00) >> 8);
        }

        public override string ToString() {
            return "{" + Material + ", " + Volume + "}";
        }

        public override bool Equals(object obj) {
            return obj is Voxel other && Equals(other);
        }
        
        public bool Equals(Voxel p) {
            return p.material == material && p.volume == volume;
        }

        public override int GetHashCode() {
            return this.Pack;
        }

        public static bool operator ==(Voxel lhs, Voxel rhs) {
            return lhs.material == rhs.material && lhs.volume == rhs.volume;
        }

        public static bool operator !=(Voxel lhs, Voxel rhs) {
            return !(lhs == rhs);
        }
    }
}