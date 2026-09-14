namespace Game.Data {
    public class ChunkCacheUpdate {
        public int chunkEntryId;
        public long version;
        public long meshVersion;
        public long soilVersion;
        public byte[] soilDenData;
        public byte[] soilMatData;
        public byte[] meshData;
        public byte[] listData;

        public long TotalLength => (soilDenData?.Length ?? 0) + 
                                   (soilMatData?.Length ?? 0) + 
                                   (meshData?.Length ?? 0) +
                                   (listData?.Length ?? 0);

        public ChunkCacheUpdate(int chunkEntryId) {
            this.chunkEntryId = chunkEntryId;
        }
    }
}