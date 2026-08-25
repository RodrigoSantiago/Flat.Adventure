using Game.Data;

namespace Code.Data {
    public class Region {
        public const int TotalLods = 3;
        public const int MaxLod = 2; // [0 1 2]
        
        public static readonly int[] LodSizeX = { 4, 2, 1};
        public static readonly int[] LodSizeY = {16, 4, 1};
        public static readonly int[] LodSizeZ = {64, 8, 1};
        public static readonly int TotalChunks = LodSizeZ[0] + LodSizeZ[1] + LodSizeZ[2];
        
        private static readonly int[] LodId = new int[TotalChunks];
        private static readonly int[] LocalId = new int[TotalChunks];

        static Region() {
            int currentLod = MaxLod;
            int localN = 0;
            for (int i = 0; i < TotalChunks; i++) {
                LodId[i] = currentLod;
                LocalId[i] = localN++;
                if (localN >= LodSizeZ[currentLod]) {
                    localN = 0;
                    currentLod--;
                }
            }
        }

        public IndexPos RegionIndex { get; }
        public readonly Chunk[][] chunks = new Chunk[TotalLods][];

        public Region(IndexPos regionIndex) {
            RegionIndex = regionIndex;
        }

        public Chunk GetChunk(int lod, int localIndex) {
            return chunks[lod][localIndex];
        }

        public void SetChunk(int lod, int localIndex, Chunk chunk) {
            chunks[lod][localIndex] = chunk;
        }

        public Chunk GetChunkByPosition(int lod, int x, int y, int z) {
            return chunks[lod][x + y * LodSizeX[lod] + z * LodSizeY[lod]];
        }

        public Chunk GetChunkByIndex(int index) {
            if (index < 0 || index >= TotalChunks) return null;
            
            return chunks[LodId[index]][LocalId[index]];
        }

        public void SetChunkByIndex(int index, Chunk chunk) {
            if (index < 0 || index >= TotalChunks) return;
            
            chunks[LodId[index]][LocalId[index]] = chunk;
        }

        public static bool IsRequired(int lod, int requiredLod) {
            return (requiredLod & (1 << lod)) == (1 << lod);
        }

        public static int GetLod(int id) {
            return id < 0 || id >= TotalChunks ? -1 : LodId[id];
        }

        public static int GetLocalId(int id) {
            return id < 0 || id >= TotalChunks ? -1 : LocalId[id];
        }

        public static IndexPos GetLocalPosition(int lod, int index) {
            int x = index % LodSizeX[lod];
            int z = index / LodSizeY[lod];
            int y = (index % LodSizeY[lod]) / LodSizeX[lod];

            return new IndexPos(x, y, z) * (ChunkSoil.Size1D * (1 << lod));
        }

        public static int GetLocalId(int lod, int x, int y, int z) {
            return x + y * LodSizeX[lod] + z * LodSizeY[lod];
        }
    }
}