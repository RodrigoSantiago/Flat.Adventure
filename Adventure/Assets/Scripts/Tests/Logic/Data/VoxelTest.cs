using Adventure.Logic.Data;
using NUnit.Framework;

namespace Tests.Logic.Data {
    public class VoxelTest {

        private static float[] byteToFloat = {
            0.0f, 0.1f, 0.2f, 0.3f, 0.4f, 0.52f, 0.55f, 0.60f, 0.65f, 0.70f, 0.75f, 0.80f, 0.85f, 0.90f, 0.95f, 1.0f
        };
        
        [Test]
        public void ToByte() {
            for (int i = 0; i <= 1000; i++) {
                float f = i / 1000f;
                Assert.AreEqual(ToByteAssert(f), Voxel.ToByte(f), "Value was (" + i + "/1000)");
            }
            for (int i = 0; i <= 1009; i++) {
                float f = i / 1009f;
                Assert.AreEqual(ToByteAssert(f), Voxel.ToByte(f), "Value was (" + i + "/1009)");
            }
        }

        [Test]
        public void ToFloat() {
            for (int i = 0; i < 16; i++) {
                Assert.AreEqual(byteToFloat[i], Voxel.ToFloat((byte) i), 1E-05, "Value was (" + i + ")");
            }
        }

        [Test]
        public void Pack() {
            for (int i = 0; i < 65535; i++) {
                var voxel = new Voxel(i);
                Assert.AreEqual(i, voxel.Pack, "Value was (" + i + ")");
            }
        }
        
        private static byte ToByteAssert(float f) {
            if (f < 0.05) return 0;
            if (f < 0.15) return 1;
            if (f < 0.25) return 2;
            if (f < 0.35) return 3;
            if (f < 0.500) return 4;
            if (f < 0.525) return 5;
            if (f < 0.575) return 6;
            if (f < 0.625) return 7;
            if (f < 0.675) return 8;
            if (f < 0.725) return 9;
            if (f < 0.775) return 10;
            if (f < 0.825) return 11;
            if (f < 0.875) return 12;
            if (f < 0.925) return 13;
            if (f < 0.975) return 14;
            return 15;
        } 
    }
}