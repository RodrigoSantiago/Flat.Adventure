namespace Adventure.Logic.Data {
    public class Block {
        
        public static readonly Block[] blocks = new Block[256];

        public short id;
        public string name;
        public string meshType; // smooth | hard | liquid
        public string textureResource;
        public string modelResource;
        // drop block
        // tool breaker
        // tool level
        // resistence
    }
}