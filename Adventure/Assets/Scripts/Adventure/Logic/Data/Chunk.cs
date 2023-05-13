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
        
        public byte[] Materials { get; private set; }
        public byte[] Volumes { get; private set; }
        
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
        
        public Chunk(Vector3Int local, Voxel[] voxels, bool original) {
            this.local = local;
            this.original = original;

            Materials = new byte[SIZE3];
            //Volumes = new byte[SIZE2H];
            Volumes = new byte[SIZE3];//
            for (int i = 0; i < voxels.Length; i+= 2) {
                var voxel = voxels[i];
                Materials[i] = voxel.material;
                Volumes[i ] |= voxel.volume;//
                //Volumes[i / 2] |= (byte)(voxel.volume & 0b1111);
            }
            for (int i = 1; i < voxels.Length; i+= 2) {
                var voxel = voxels[i];
                Materials[i] = voxel.material;
                Volumes[i] |= voxel.volume;//
                //Volumes[i / 2] |= (byte)(voxel.volume << 4);
            }
        }

        public Voxel Get(int x, int y, int z) {
            return Get(x + y * SIZE + z * SIZE2);
        }

        public Voxel Get(int index) {
            byte material = Materials[index];
            //byte volume = (byte)(index % 2 == 0 ? Volumes[index / 2] & 0b1111 : Volumes[index / 2] >> 4);
            byte volume = Volumes[index];//
            return new Voxel(material, volume);
        }

        public void Set(int x, int y, int z, Voxel voxel) {
            this.Set(x + y * SIZE + z * SIZE2, voxel);
        }

        public void Set(int index, Voxel voxel) {
            original = false;
            Materials[index] = voxel.material;
            Volumes[index] = voxel.volume;
            /*if (index % 2 == 0) {
                Volumes[index / 2] &= 0b11110000;
                Volumes[index / 2] |= (byte)(voxel.volume & 0b1111);
            } else {
                Volumes[index / 2] &= 0b00001111;
                Volumes[index / 2] |= (byte)(voxel.volume << 4);
            }*/
        }
    }
}
