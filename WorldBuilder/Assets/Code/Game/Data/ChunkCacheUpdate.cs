namespace Code.Data {
    public class ChunkCacheUpdate {
        public int chunkEntryId;
        public int version;
        public byte[] soilData;
        public byte[] meshData;
        public byte[] listData;

        public int TotalLength => soilData?.Length ?? 0 + meshData?.Length ?? 0 + listData?.Length ?? 0;
    }
}