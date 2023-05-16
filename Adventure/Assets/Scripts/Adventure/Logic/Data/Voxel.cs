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
        
        public byte mat;
        public byte vol;

        public int material {
            get { return mat; }
        }

        public float volume {
            get { return ToFloat(vol); }
        }

        public ushort pack {
            get { return (ushort)((vol << 8) | mat); }
        }

        public Voxel(byte mat, byte vol) {
            this.mat = mat;
            this.vol = vol;
        }

        public Voxel(int material, float volume) {
            this.mat = (byte)material;
            this.vol = ToByte(volume);
        }

        public Voxel(int packData) {
            this.mat = (byte)(packData & 0xFF);
            this.vol = (byte)((packData & 0xFF00) >> 8);
        }

        public override string ToString() {
            return "{" + material + ", " + volume + "}";
        }

        public override bool Equals(object obj) {
            return obj is Voxel other && Equals(other);
        }
        
        public bool Equals(Voxel p) {
            return p.mat == mat && p.vol == vol;
        }

        public override int GetHashCode() {
            return this.pack;
        }

        public static bool operator ==(Voxel lhs, Voxel rhs) {
            return lhs.mat == rhs.mat && lhs.vol == rhs.vol;
        }

        public static bool operator !=(Voxel lhs, Voxel rhs) {
            return !(lhs == rhs);
        }
    }
}