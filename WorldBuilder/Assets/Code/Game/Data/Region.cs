using System.Linq;

namespace Game.Data {
    public class Region {
        public const int TotalLods = 4;
        public const int MaxLod = 3; // [0 1 2 3]
        
        public static readonly int[] LodSize1 = {8, 4, 2, 1};
        public static readonly int[] LodSize2 = {64, 16, 4, 1};
        public static readonly int[] LodSize3 = {512, 64, 8, 1};
        public static readonly int TotalChunks = LodSize3.Sum();
        
        private static readonly int[] LodId = new int[TotalChunks];
        private static readonly int[] LocalId = new int[TotalChunks];
        private static readonly int[] LodCount = {0, 512, 576, 584};

        static Region() {
            int currentLod = 0;
            int localN = 0;
            for (int i = 0; i < TotalChunks; i++) {
                LodId[i] = currentLod;
                LocalId[i] = localN++;
                if (localN >= LodSize3[currentLod]) {
                    localN = 0;
                    currentLod++;
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
            return chunks[lod][GetLocalId(lod, x, y, z)];
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
            int x = index % LodSize1[lod];
            int z = (index % LodSize2[lod]) / LodSize1[lod];
            int y = index / LodSize2[lod];

            return new IndexPos(x, y, z) * (ChunkSoil.Size1D * (1 << lod));
        }

        public static int GetId(int lod, int x, int y, int z) {
            return GetLocalId(lod, x, y, z) + LodCount[lod];
        }

        public static int GetId(int lod, IndexPos pos) {
            return GetLocalId(lod, pos) + LodCount[lod];
        }

        public static int GetLocalId(int lod, IndexPos pos) {
            return GetLocalId(lod, pos.x, pos.y, pos.z);
        }

        public static int GetLocalId(int lod, int x, int y, int z) {
            return   x / (32 << lod) 
                   + z / (32 << lod) * LodSize1[lod] 
                   + y / (32 << lod) * LodSize2[lod];
        }
    }
}