using System.Runtime.InteropServices;
using Code;
using Code.Data;
using Code.Worlds;
using Code.Worlds.Storage;
using Game.Worlds.Storage;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Data {
    public class Chunk {
        
        public IndexPos Pos { get; private set; }
        
        public int Lod { get; private set; }
        public int Version { get; set; }
        public int CurrentVersion { get; private set; }
        public int MeshVersion { get; private set; }
        public ChunkSoil Soil { get; private set; }
        public Mesh SoilMesh { get; private set; }
        public bool Released { get; set; } = true;

        public Chunk(IndexPos pos, int lod) {
            Pos = pos;
            Lod = lod;
        }

        public Chunk(IndexPos pos, int lod, ChunkCacheUpdate cache) {
            Pos = pos;
            Lod = lod;
        }
        
        public Chunk(IndexPos pos, int lod, Chunk[] lowerLod) {
            Pos = pos;
            Lod = lod;
            
            // Version = Max(lowerLod.Version)
        }
        
        // public List<Structure> structures; // A structure has a chunkData special number [15] for LOD > 0
        // public List<Prop> props;           // Props are interactive objects, not visible on LOD > 0
        // public List<Item> items;           // Temporary collectables items
        // public List<Unit> units;           // Any Mob, Boss, Npc and similar
        // public List<Tree> trees;           // Tree can be visible at any LOD
        // public List<Grass> grass;          // Grass uses a special rendering method

        public void RequestExport(WriteStorageData output) {
            if (SoilMesh != null) {
                GraphicsBuffer buffer = SoilMesh.GetVertexBuffer(0);

                AsyncGPUReadback.Request(buffer, request => {
                    if (request.hasError) {
                        buffer.Dispose();
                        WorldManager.Instance.RunTask(() => RequestData(output, null));
                        return;
                    }

                    var vertices = request.GetData<GeneratedVertexLow>();
                    
                    byte[] bytes = vertices.Reinterpret<byte>(Marshal.SizeOf<GeneratedVertexLow>()).ToArray();
                    buffer.Dispose();
                    
                    WorldManager.Instance.RunTask(() => RequestData(output, bytes));
                });
            } else {
                WorldManager.Instance.RunTask(() => RequestData(output, null));
            }
        }

        private void RequestData(WriteStorageData output, byte[] mesh) {
            var update = new ChunkCacheUpdate();
            update.chunkEntryId = Region.GetLocalId(Lod, Pos - Pos.GetChunkIndex(Region.MaxLod));
            update.version = CurrentVersion;
            
            if (mesh != null && MeshVersion == CurrentVersion) {
                update.meshData = mesh;
            }

            if (Soil != null) {
                update.soilDenData = Soil.ExportDensity();
                update.soilMatData = Soil.ExportMaterial();
            }
            
            output.PutData(Pos, update);
        }
    }
}