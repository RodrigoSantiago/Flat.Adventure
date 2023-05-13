using System.Runtime.InteropServices;
using UnityEngine;

namespace Adventure.Logic.Data {
    
    [StructLayout(LayoutKind.Sequential)]
    public struct Voxel {

        /*private static float[] byteToFloat = {
            0.0f,
            0.1f,
            0.2f,
            0.3f,
            0.4f,
            0.52f,
            0.55f,
            0.60f,
            0.65f,
            0.70f,
            0.75f,
            0.80f,
            0.85f,
            0.90f,
            0.95f,
            1.0f
        };*/
        
        private static float[] byteToFloat = {
            0.000f,
            0.066f,
            0.132f,
            0.198f,
            0.264f,
            0.330f,
            0.396f,
            0.462f,
            0.528f,
            0.594f,
            0.660f,
            0.726f,
            0.792f,
            0.858f,
            0.924f,
            1.000f
        };
        
        public static float ToFloat(byte b) {
            return (b / 1 * 1) / 255f; //byteToFloat[b];
        }

        public static byte ToByte(float f) {
            byte b= (byte)Mathf.RoundToInt(Mathf.Clamp01(f) * 255);
            return (byte)(b / 1 * 1);
            for (int i = 1; i < byteToFloat.Length; i++) {
                if (f < (byteToFloat[i - 1] + byteToFloat[i]) / 1) return (byte)(i - 1);
            }

            return 15;
            if (f < 0.35) return (byte) ((f + 0.05f) * 10);
            if (f < 0.525) return (byte) (5 - (((uint) (f * 100) - 50) >> 31));
            if (f < 0.975) return (byte) (((int) (f * 1000) - 525) / 50 + 6);
            return 15;
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