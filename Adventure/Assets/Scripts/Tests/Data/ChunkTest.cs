using System.Collections.Generic;
using Adventure.Data;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Data {
    public class ChunkTest {
        
        private Voxel[] voxels;
        
        [SetUp]
        public void SetUp() {
            voxels = new Voxel[16 * 16 * 16];
            for (int i = 0; i < 16 * 16 * 16; i++) {
                voxels[i] = new Voxel(i % 64, i % 64 == 0 ? 0 : i % 13 == 1 ? 0.6f : 1f);
            }
        }

        [Test]
        public void Chunk() {
            Chunk chunk = new Chunk(new Vector3Int(0, 0, 0), voxels, true);

            for (int i = 0; i < 16 * 16 * 16; i++) {
                int x = i / 256;
                int y = (i % 256) / 16;
                int z = i % 16;
                Assert.AreEqual(voxels[i], chunk.Get(i), "Unexpected Voxel At " + x + ", " + y + ", " + z);
            }
        }

        [Test]
        public void GetSetIndexer() {
            Chunk chunk = new Chunk(new Vector3Int(0, 0, 0), voxels, true);
            Voxel newVoxel = new Voxel(4, 0.6f);
            
            chunk[10, 10, 10] = newVoxel;
            Assert.IsFalse(chunk.IsOriginal, "After edited a Chunk could not be Original");
            Assert.AreEqual(newVoxel, chunk[10, 10, 10], "Unexpected Voxel At " + 10 + ", " + 10 + ", " + 10);
            
            for (int i = 0; i < 16 * 16 * 16; i++) {
                int x = i / 256;
                int y = (i % 256) / 16;
                int z = i % 16;
                if (x == 10 && y == 10 && z == 10) {
                    Assert.AreEqual(newVoxel, chunk.Get(i), "Unexpected Voxel At " + x + ", " + y + ", " + z);
                } else {
                    Assert.AreEqual(voxels[i], chunk.Get(i), "Unexpected Voxel At " + x + ", " + y + ", " + z);
                }
            }
        }
        
    }
}
