namespace Game.Data {
    public class ChunkCachePair {
        public Chunk Chunk { get; }
        public ChunkCacheUpdate Update { get; }
        
        public ChunkCachePair(Chunk chunk, ChunkCacheUpdate update) {
            Chunk = chunk;
            Update = update;
        }
    }
}