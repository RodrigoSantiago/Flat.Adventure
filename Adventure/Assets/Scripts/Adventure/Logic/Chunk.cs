namespace Adventure.Logic {
    public class Chunk {
        
        private Palette palette;
        private byte[] data;
        
        public Point3 Local { get; private set; }
        public bool IsIntact { get; private set; }

        public Chunk(Palette palette, short[] blocks, byte[] volume) {
            
        }

        public Voxel read(int x, int y, int z) {
            
            return default;
        }
    }
}
