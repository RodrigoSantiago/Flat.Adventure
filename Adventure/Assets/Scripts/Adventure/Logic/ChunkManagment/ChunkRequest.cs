using System.Collections.Generic;
using Adventure.Logic.Data;
using UnityEngine;

namespace Adventure.Logic.ChunkManagment {
    public class ChunkRequest {
        public readonly Vector3Int local;
        public readonly int lod;
        public List<WorldPlayerController> listeners;

        public Chunk chunk { get; private set; }
        private Chunk[] chunkForLod;
        private int readyChunks;

        public ChunkRequest(Vector3Int local, int lod) {
            this.local = local;
            this.lod = lod;
            this.chunkForLod = lod == 0 ? null : new Chunk[8];
        }

        public void AddListener(WorldPlayerController listener) {
            if (listener == null) return;

            listeners ??= new List<WorldPlayerController>();
            listeners.Add(listener);
        }

        public void AddChunk(Chunk chunk) {
            if (lod == 0) {
                this.chunk = chunk;
            } else {
                var off = (chunk.local - this.local) / (16 * (1 << (lod - 1)));
                int index = off.z * 4 + off.y * 2 + off.x;
                chunkForLod[index] = chunk;
                readyChunks++;
            }
        }

        public bool IsReady() {
            return lod == 0 ? chunk != null : readyChunks >= 8;
        }

        private static readonly Vector3Int[] looper = {
            new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0),
            new Vector3Int(0, 1, 0), new Vector3Int(1, 1, 0),
            new Vector3Int(0, 0, 1), new Vector3Int(1, 0, 1),
            new Vector3Int(0, 1, 1), new Vector3Int(1, 1, 1),
        };

        public void PackChunks() {
            if (lod == 0) return;
            
            Voxel[] voxels = new Voxel[16 * 16 * 16];
            Voxel single = chunkForLod[0][0];
            bool isSingle = true;
            int i = 0;
            
            for (int p = 0; p < 8; p++) {
                var pt = looper[p];
                var lChunk = chunkForLod[i];
                for (int iz = 0; iz < 16; iz += 2)
                for (int iy = 0; iy < 16; iy += 2)
                for (int ix = 0; ix < 16; ix += 2) {
                    Voxel vox = lChunk[ix, iy, iz];
                    if (vox.volume != 1 && vox.volume != 0) {
                        vox = new Voxel((vox.volume + 0.5f) * 0.5f, vox.material);
                    }
                    voxels[(pt.x * 8 + ix / 2) + (pt.y * 8 + iy / 2) * 16 + (pt.z * 8 + iz / 2) * 256] = vox;
                    if (isSingle && vox != single) {
                        isSingle = false;
                    }
                }

                i++;
            }

            chunk = isSingle ? new Chunk(this.local, lod, true, single) : new Chunk(this.local, lod, true, voxels);
        }
    }
}