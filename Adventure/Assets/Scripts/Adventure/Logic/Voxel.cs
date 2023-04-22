namespace Adventure.Logic {
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
        
        public short block;
        public float distance;
    }
}