using System.Runtime.InteropServices;

namespace Adventure.Logic.Data {
    
    [StructLayout(LayoutKind.Sequential)]
    public struct Voxel {

        private static float[] byteToFloat = {
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
        };

        public static float ToFloat(byte b) {
            return byteToFloat[b];
        }

        public static byte ToByte(float f) {
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
            return "{Mat:" + Material + ", Vol: " + Volume + "}";
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