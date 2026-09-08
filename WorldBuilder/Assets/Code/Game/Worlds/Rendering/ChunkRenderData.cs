using Game.Data;
using Game.GraphicGenerator;

namespace Game.Worlds.Rendering {
    public class ChunkRenderData {

        private readonly WorldManager manager;
        
        public Chunk Chunk { get; private set; }
        public MeshInterface Mesh { get; private set; }
        public bool IsReleased => Chunk == null;

        public IndexPos Pos { get; }
        public int Lod { get; }
        public bool IsEmpty { get; private set; }
        public bool IsBuildRequired { get; private set; }
        
        private Chunk[] chunks;
        
        public bool ChunkReleased { get; set; }

        public ChunkRenderData(WorldManager manager, Chunk chunk) {
            this.manager = manager;
            Chunk = chunk;
            Pos = chunk.Pos;
            Lod = chunk.Lod;
            IsEmpty = chunk.Soil.IsEmpty();
        }

        public void Dispose() {
            if (Mesh != null) {
                Mesh.Dispose();
            }
        }

        public void RefreshChunk(Chunk chunk) {
            Chunk = chunk;
            ChunkReleased = false;
        }

        public void RequestMesh() {
            if (IsBuildRequired || !manager.IsChunkRenderRequired(Lod, Pos)) return;
            
            chunks ??= new Chunk[27];
            for (int j = 0; j < 27; j++) {
                chunks[j] = null;
            }
            
            int i = 0;
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
            for (int x = -1; x <= 1; x++) {
                var pos = Pos + new IndexPos(x, y, z) * (32 << Lod);
                if (pos.x < 0 || pos.y < 0 || pos.z < 0) {
                    chunks[i++] = null;
                } else {
                    var near = manager.FindChunkData(Lod, pos);
                    if (near == null) {
                        return;
                    } else if (near.IsReleased) {
                        manager.RequestChunk(Lod, pos);
                        return;
                    }

                    chunks[i++] = near.Chunk;
                }
            }

            IsBuildRequired = true;
            chunks[13] = Chunk;
            
            manager.Render.RequestMesh(chunks, (mesh) => {
                Mesh = mesh;
                if (mesh == null) {
                    IsEmpty = true;
                }
                // Chunk.SoilMesh = mesh;
                
                for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++)
                for (int x = -1; x <= 1; x++) {
                    var pos = Pos + new IndexPos(x, y, z) * (32 << Lod);
                    if (pos.x < 0 || pos.y < 0 || pos.z < 0) continue;
                    
                    var near = manager.FindChunkData(Lod, pos);
                    if (near != null) {
                        near.ReleaseData();
                    }
                }
            });
            
            chunks = null;
        }

        private void ReleaseData() {
            if (Chunk != null && !IsDataRequired()) {
                 Chunk = null;
            }
        }

        private bool IsDataRequired() {
            if (manager.IsChunkRenderRequired(Lod, Pos) && !IsBuildRequired) return true;
            
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
            for (int x = -1; x <= 1; x++) {
                if (x == 0 && y == 0 && z == 0) continue;
                
                var pos = Pos + new IndexPos(x, y, z) * (32 << Lod);
                if (pos.x >= 0 && pos.y >= 0 && pos.z >= 0) {
                    if (!manager.IsChunkRenderRequired(Lod, pos)) {
                        continue;
                    }
                    
                    var near = manager.FindChunkData(Lod, pos);
                    if (near == null || !near.IsBuildRequired) {
                        return true;
                    }
                }
            }
            
            return false;
        }
    }
}