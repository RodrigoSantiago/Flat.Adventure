using System.Collections.Generic;
using Adventure.Logic.Data;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Logic.Data {
    public class ChunkTest {
        
        private Voxel[] voxels;
        
        [SetUp]
        public void SetUp() {
            voxels = new Voxel[16 * 16 * 16];
            for (int i = 0; i < 16 * 16 * 16; i++) {
                voxels[i] = new Voxel(i % 64 == 0 ? 0 : i % 13 == 1 ? 0.6f : 1f, i % 64);
            }
        }

        [Test]
        public void Chunk() {
            Chunk chunk = new Chunk(new Vector3Int(0, 0, 0), 0, true, voxels);

            for (int i = 0; i < 16 * 16 * 16; i++) {
                int x = i % 16;
                int y = (i % 256) / 16;
                int z = i / 256;
                Assert.AreEqual(voxels[i], chunk.Get(i), "Unexpected Voxel At " + x + ", " + y + ", " + z);
            }
        }

        [Test]
        public void GetSetIndexer() {
            Chunk chunk = new Chunk(new Vector3Int(0, 0, 0), 0, true, voxels);
            Voxel newVoxel = new Voxel(0.6f, 4);
            
            chunk[10, 10, 10] = newVoxel;
            Assert.IsFalse(chunk.original, "After edited a Chunk could not be Original");
            Assert.AreEqual(newVoxel, chunk[10, 10, 10], "Unexpected Voxel At " + 10 + ", " + 10 + ", " + 10);
            
            for (int i = 0; i < 16 * 16 * 16; i++) {
                int x = i % 16;
                int y = (i % 256) / 16;
                int z = i / 256;
                if (x == 10 && y == 10 && z == 10) {
                    Assert.AreEqual(newVoxel, chunk.Get(i), "Unexpected Voxel At " + x + ", " + y + ", " + z);
                } else {
                    Assert.AreEqual(voxels[i], chunk.Get(i), "Unexpected Voxel At " + x + ", " + y + ", " + z);
                }
            }
        }
        
    }
}
