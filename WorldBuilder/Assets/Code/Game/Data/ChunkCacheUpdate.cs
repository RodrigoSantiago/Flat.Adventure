namespace Game.Data {
    public class ChunkCacheUpdate {
        public int chunkEntryId;
        public int version;
        public byte[] soilDenData;
        public byte[] soilMatData;
        public byte[] meshData;
        public byte[] listData;

        public long TotalLength => (soilDenData?.Length ?? 0) + 
                                   (soilMatData?.Length ?? 0) + 
                                   (meshData?.Length ?? 0) +
                                   (listData?.Length ?? 0);
    }
}