using UnityEngine;

namespace Adventure.Logic.Data {
    
    /**
     * A chunk represent a group of world data to create the ground and water
     */
    public class Chunk {
        private const int SIZE = 16;
        private const int SIZE2 = 256;
        private const int SIZE2H = 2048;
        private const int SIZE3 = 4096;

        public readonly int lod; 
        public byte[] materials { get; private set; }
        public byte[] volumes { get; private set; }
        
        private byte baseMaterial;
        private byte baseVolume;
        
        public Vector3Int local { get; }
        public bool original { get; private set; }

        public Voxel this[int x, int y, int z] {
            get { return Get(x, y, z); }
            set { Set(x, y, z, value); }
        }

        public Voxel this[Vector3Int pos] {
            get { return Get(pos.x, pos.y, pos.z); }
            set { Set(pos.x, pos.y, pos.z, value); }
        }

        public Voxel this[int index] {
            get { return Get(index); }
            set { Set(index, value); }
        }
        
        public Chunk(Vector3Int local, int lod, bool original, params Voxel[] voxels) {
            this.local = local;
            this.lod = lod;
            this.original = original;

            materials = new byte[SIZE3];
            volumes = new byte[SIZE2H];
            for (int i = 0; i < voxels.Length; i+= 2) {
                var voxel = voxels[i];
                materials[i] = voxel.mat;
                volumes[i / 2] |= (byte)(voxel.vol & 0b1111);
            }
            for (int i = 1; i < voxels.Length; i+= 2) {
                var voxel = voxels[i];
                materials[i] = voxel.mat;
                volumes[i / 2] |= (byte)(voxel.vol << 4);
            }
        }

        public Chunk(Vector3Int local, int lod, bool original, Voxel voxel) {
            this.local = local;
            this.lod = lod;
            this.original = original;
            this.baseMaterial = voxel.mat;
            this.baseVolume = voxel.vol;
        }

        public Voxel Get(int x, int y, int z) {
            return Get(x + y * SIZE + z * SIZE2);
        }

        public Voxel Get(int index) {
            byte material = materials == null ? baseMaterial : materials[index];
            byte volume = volumes == null ? baseVolume : (byte)(index % 2 == 0 ? volumes[index / 2] & 0b1111 : volumes[index / 2] >> 4);
            return new Voxel(volume, material);
        }

        public void Set(int x, int y, int z, Voxel voxel) {
            this.Set(x + y * SIZE + z * SIZE2, voxel);
        }

        public void Set(int index, Voxel voxel) {
            SetForWrite();
            
            materials[index] = voxel.mat;
            if (index % 2 == 0) {
                volumes[index / 2] &= 0b11110000;
                volumes[index / 2] |= (byte)(voxel.vol & 0b1111);
            } else {
                volumes[index / 2] &= 0b00001111;
                volumes[index / 2] |= (byte)(voxel.vol << 4);
            }
        }

        public bool IsEmpty() {
            return materials == null && volumes == null && baseVolume == 0;
        }

        public bool IsSingle() {
            return materials == null && volumes == null;
        }

        public void Recalculate() {
            byte baseMat = materials[0];
            for (int i = 1; i < SIZE3; i++) {
                if (materials[i] != baseMat) return;
            }
            byte baseVol = volumes[0];
            for (int i = 1; i < SIZE2H; i++) {
                if (volumes[i] != baseVol) return;
            }

            materials = null;
            volumes = null;
            baseMaterial = baseMat;
            baseVolume = baseVol;
        }

        private void SetForWrite() {
            original = false;
            if (materials == null) {
                materials = new byte[SIZE3];
            }
            if (volumes == null) {
                volumes = new byte[SIZE2H];
            }
        }
    }
}
