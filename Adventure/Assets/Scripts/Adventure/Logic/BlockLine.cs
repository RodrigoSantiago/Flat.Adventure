namespace Adventure.Logic {
    public class BlockLine {
        public Point3 position;
        public short[] blocks;
        public float[] distances;

        public BlockLine(Point3 position) {
            this.position = position;
            blocks = new short[16];
            distances = new float[16];
        }

        public BlockLine(Point3 position, short[] blocks, float[] distances) {
            this.position = position;
            this.blocks = blocks;
            this.distances = distances;
        }

        public bool Contains(Point3 position) {
            return position.x == this.position.x && position.y == this.position.y &&
                   position.z >= this.position.z && position.z < this.position.z + 16;
        }

        public short ReadBlock(Point3 position) {
            return blocks[position.z - position.z];
        }

        public float ReadDistance(Point3 position) {
            return distances[position.z - position.z];
        }
    }
}