using Game.Data;
using Game.GraphicGenerator;
using UnityEngine;

namespace Game.Worlds.Rendering {
    public class ChunkRenderData {

        private readonly WorldManager manager;
        
        public Chunk Chunk { get; private set; }
        public MeshInterface SoilMesh { get; private set; }
        public bool IsReleased => Chunk == null;

        public IndexPos Pos { get; }
        public int Lod { get; }
        public bool IsEmpty { get; private set; }
        public bool IsBuildRequired { get; private set; }
        
        private Chunk[] chunks;

        public bool disposed;
        public bool ChunkReleased { get; set; }

        public ChunkRenderData(WorldManager manager, Chunk chunk) {
            this.manager = manager;
            Chunk = chunk;
            Pos = chunk.Pos;
            Lod = chunk.Lod;
            IsEmpty = chunk.Soil.IsEmpty();
        }

        public void Dispose() {
            disposed = true;
            SoilMesh?.RemoveReference();
            SoilMesh = null;
            Chunk = null;
        }

        public void RefreshChunk(Chunk chunk) {
            Chunk = chunk;
            ChunkReleased = false;
        }

        public void RequestMesh() {
            if (IsBuildRequired || !manager.IsChunkRenderRequired(Lod, Pos)) return;

            if (Chunk != null && Chunk.SoilMesh != null) {
                IsBuildRequired = true;
                SoilMesh = Chunk.SoilMesh;
                SoilMesh.AddReference();
                return;
            } 
            if (Chunk != null && Chunk.CanCreateMesh()) {
                IsBuildRequired = true;
                GameManager.Instance.RunTask(() => {
                    if (Chunk.CreateMesh() || Chunk.SoilMesh != null) {
                        SoilMesh = Chunk.SoilMesh;
                        SoilMesh.AddReference();
                    } else {
                        IsBuildRequired = false;
                        RequestMesh();
                    }
                });
                return;
            }
            
            chunks ??= new Chunk[27];

            bool incomplete = false;
            int i = 0;
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
            for (int x = -1; x <= 1; x++) {
                int index = i++;
                var pos = Pos + new IndexPos(x, y, z) * (32 << Lod);
                if (pos.x < 0 || pos.y < 0 || pos.z < 0) {
                    chunks[index] = null;
                } else {
                    var near = manager.FindChunkData(Lod, pos);
                    if (near == null || near.IsReleased) {
                        manager.RequestChunk(Lod, pos);
                        incomplete = true;
                        continue;
                    }
                    
                    chunks[index] = near.Chunk;
                }
            }

            if (incomplete) {
                for (int j = 0; j < chunks.Length; j++) {
                    chunks[j] = null;
                }
                return;
            }

            IsBuildRequired = true;
            chunks[13] = Chunk;
            
            manager.Render.RequestMesh(chunks, (mesh) => {
                if (mesh == null) {
                    IsEmpty = true;
                } else {
                    if (disposed) {
                        mesh.Dispose();
                    } else {
                        SoilMesh = mesh;
                        SoilMesh.AddReference();
                        if (Chunk != null) {
                            Chunk.SoilMesh = mesh;
                            Chunk.SoilMesh.AddReference();
                            Chunk.Modified = true;
                        }
                    }
                }
                
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