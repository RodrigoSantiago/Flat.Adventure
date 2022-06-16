namespace Adventure.Logic {
    public class Block {
        
        public static readonly Block[] blocks = new Block[4096];

        public short id;
        public string name;
        public string meshType; // smooth | hard | liquid | cube | mesh
        public bool solid;
        public bool opaque;
        public string textureResource;
        public string modelResource;
        // drop block
        // tool breaker
        // tool level
        // resistence
    }
}