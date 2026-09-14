using Game.Data;
using Game.GraphicGenerator;
using UnityEngine;

namespace Game.Worlds.Rendering {
    public class ChunkRenderData {

        private readonly WorldManager manager;
        
        public Chunk Chunk { get; private set; }
        public bool IsReleased => Chunk == null;

        private MeshInterface soilMesh;

        public MeshInterface SoilMesh {
            get => soilMesh;
            private set {
                if (value != soilMesh) {
                    soilMesh?.RemoveReference();
                    soilMesh = value;
                    soilMesh?.AddReference();
                }
            }
        }

        public IndexPos Pos { get; }
        public int Lod { get; }
        public bool IsEmpty { get; private set; }
        public bool IsMeshBuilding { get; private set; }
        public bool IsMeshComplete { get; private set; }
        
        private Chunk[] chunks;

        public bool disposed;
        public bool ChunkReleased { get; set; }

        public ChunkRenderData(WorldManager manager, Chunk chunk) {
            this.manager = manager;
            Chunk = chunk;
            Pos = chunk.Pos;
            Lod = chunk.Lod;
            IsEmpty = chunk.Soil.IsEmpty();

            if (GameManager.Instance.DebugChunkPosition) {
                var obj = new GameObject(Region.GenName(Pos) + "[" + Lod + "]");
                obj.transform.position = (Vector3)Pos;
                obj.transform.localScale = Vector3.one * (1 << Lod);
                var box = obj.AddComponent<BoxCollider>();
                box.center = new Vector3(16, 16, 16);
                box.size = new Vector3(32, 32, 32);
            } 
        }

        public void Dispose() {
            disposed = true;
            SoilMesh = null;
            Chunk = null;
        }

        public void RefreshChunk(Chunk chunk) {
            Chunk = chunk;
            ChunkReleased = false;
        }

        public void RequestMesh() {
            if (IsMeshComplete || IsMeshBuilding || !manager.IsChunkRenderRequired(Lod, Pos)) return;

            if (Chunk != null && Chunk.SoilMesh != null) {
                Debug.Log("Chunk.SoilMesh");
                
                SoilMesh = Chunk.SoilMesh;
                PostMesh();
                return;
            } 
            
            if (Chunk != null && Chunk.CanCreateMesh()) {
                Debug.Log("CanCreateMesh");
                
                IsMeshBuilding = true;
                GameManager.Instance.RunTask(() => {
                    if (disposed) return;
                    
                    if (Chunk.CreateMesh() || Chunk.SoilMesh != null) {
                        SoilMesh = Chunk.SoilMesh;
                        PostMesh();
                    } else {
                        Debug.Log("Failed");
                        IsMeshBuilding = false;
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

            chunks[13] = Chunk;
            long version = Chunk?.CurrentSoilVersion ?? 0;
            
            IsMeshBuilding = true;
            manager.Render.RequestMesh(chunks, (mesh) => {
                Debug.Log("RequestMesh");
                
                if (disposed) {
                    mesh?.Dispose();
                    return;
                }
                
                if (mesh == null) {
                    IsEmpty = true;
                    Debug.Log("Null Mesh");
                    
                } else {
                    SoilMesh = mesh;
                    if (Chunk != null) {
                        Chunk.EditChunkMesh(version, mesh);
                    }
                }
                
                PostMesh();
            });
            
            chunks = null;
        }

        private void PostMesh() {
            IsMeshComplete = true;
            
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
        }

        public void ReleaseData() {
            if (Chunk != null && !IsDataRequired()) { 
                Chunk = null;
            }
        }

        private bool IsDataRequired() {
            if (manager.IsChunkRenderRequired(Lod, Pos) && !IsMeshComplete) return true;
            
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
                    if (near == null || !near.IsMeshBuilding) {
                        return true;
                    }
                }
            }
            
            return false;
        }
    }
}