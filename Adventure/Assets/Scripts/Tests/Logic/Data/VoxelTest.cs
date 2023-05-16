using System;
using Adventure.Logic.Data;
using NUnit.Framework;

namespace Tests.Logic.Data {
    public class VoxelTest {
        
        [Test]
        public void ToByte() {
            for (int i = 0; i <= 1009; i++) {
                float f = i / 1009f;
                Assert.AreEqual((byte)MathF.Round(f * 15), Voxel.ToByte(f), "Value was (" + i + "/1009)");
            }
        }

        [Test]
        public void ToFloat() {
            for (int i = 0; i < 16; i++) {
                Assert.AreEqual(i / 15f, Voxel.ToFloat((byte) i), 1E-05, "Value was (" + i + ")");
            }
        }

        [Test]
        public void Pack() {
            for (int i = 0; i < 65535; i++) {
                var voxel = new Voxel(i);
                Assert.AreEqual(i, voxel.pack, "Value was (" + i + ")");
            }
        }
    }
}