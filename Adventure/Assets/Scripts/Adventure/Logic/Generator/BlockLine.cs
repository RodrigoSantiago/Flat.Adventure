using UnityEngine;

namespace Adventure.Logic.Generator {
    public class BlockLine {
        public Vector3Int position;
        public short[] blocks;
        public float[] distances;

        public BlockLine(Vector3Int position) {
            this.position = position;
            blocks = new short[16];
            distances = new float[16];
        }

        public BlockLine(Vector3Int position, short[] blocks, float[] distances) {
            this.position = position;
            this.blocks = blocks;
            this.distances = distances;
        }

        public bool Contains(Vector3Int position) {
            return position.x == this.position.x && position.y == this.position.y &&
                   position.z >= this.position.z && position.z < this.position.z + 16;
        }

        public short ReadBlock(Vector3Int position) {
            return blocks[position.z - position.z];
        }

        public float ReadDistance(Vector3Int position) {
            return distances[position.z - position.z];
        }
    }
}