using System;
using Game.GraphicGenerator;
using Game.Worlds.Storage;

namespace Game.Data {
    public class Chunk {
        
        public IndexPos Pos { get; private set; }
        
        public int Lod { get; private set; }
        public int Version { get; set; }
        public int CurrentVersion { get; set; }
        public int MeshVersion { get; private set; }
        public ChunkSoil Soil { get; private set; }
        public MeshInterface SoilMesh { get; set; }
        public bool Released { get; set; }

        public bool Modified { get; set; }
        private byte[] soilMeshTempData;

        public Chunk(IndexPos pos, int lod) {
            Pos = pos;
            Lod = lod;
        }
        
        public Chunk(IndexPos pos, int lod, ChunkSoil soil) {
            Pos = pos;
            Lod = lod;
            Soil = soil;
        }

        public Chunk(IndexPos pos, int lod, ChunkCacheUpdate cache) {
            Pos = pos;
            Lod = lod;
            Soil = new ChunkSoil(cache.soilDenData, cache.soilMatData);
            soilMeshTempData = cache.meshData;
            CurrentVersion = cache.version;
            Version = CurrentVersion;
        }
        
        public Chunk(IndexPos pos, int lod, Chunk[] lowerLod) {
            Pos = pos;
            Lod = lod;
            Soil = GenerateLod(lowerLod);
            foreach (var lChunk in lowerLod) {
                CurrentVersion = Math.Max(CurrentVersion, lChunk.CurrentVersion);
                Version = Math.Max(Version, lChunk.Version);
            }
        }

        public bool CanCreateMesh() {
            return soilMeshTempData != null;
        }

        public bool CreateMesh() {
            if (soilMeshTempData != null) {
                try {
                    SoilMesh = new MeshInterface(Pos, Lod, soilMeshTempData);
                    SoilMesh.AddReference();
                } catch {
                    return false;
                } finally {
                    soilMeshTempData = null;
                }
                
                return true;
            }
            return false;
        }

        private static ChunkSoil GenerateLod(Chunk[] chunks) {
            var result = new ChunkSoil();
            result.ExpandMaterial();

            for (int z = 0; z < 32; z++)
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++) {
                int globalX = x << 1;
                int globalY = y << 1;
                int globalZ = z << 1;

                int chunkX = globalX >> 5;
                int chunkY = globalY >> 5;
                int chunkZ = globalZ >> 5;

                int index = chunkX | (chunkZ << 1) | (chunkY << 2);
                
                int localX = globalX & 31;
                int localY = globalY & 31;
                int localZ = globalZ & 31;

                var soil = chunks[index].Soil;

                float density = soil.GetDensity(localX, localY, localZ);

                density = Math.Clamp(0.5f + (density - 0.5f) * 2.0f, 0.0f, 1.0f);

                result.SetDensity(x, y, z, density);
                result.SetMaterial(x, y, z, soil.GetMaterial(localX, localY, localZ));
            }

            return result;
        }

        // public List<Structure> structures; // A structure has a chunkData special number [15] for LOD > 0
        // public List<Prop> props;           // Props are interactive objects, not visible on LOD > 0
        // public List<Item> items;           // Temporary collectables items
        // public List<Unit> units;           // Any Mob, Boss, Npc and similar
        // public List<Tree> trees;           // Tree can be visible at any LOD
        // public List<Grass> grass;          // Grass uses a special rendering method

        public ChunkCacheUpdate RequestExport() {
            var update = new ChunkCacheUpdate();
            update.chunkEntryId = Region.GetId(Lod, Pos - Pos.GetChunkIndex(Region.MaxLod));
            update.version = CurrentVersion;
            update.soilDenData = Soil.ExportDensity();
            update.soilMatData = Soil.ExportMaterial();
            update.meshData = SoilMesh?.RequestData();
            return update;
        }

        public void RequestExportAsync(WriteStorageData output) {
            if (SoilMesh != null) {
                SoilMesh.RequestDataAsync(data => {
                    GameManager.Instance.RunTask(() => RequestData(output, data));
                });
            } else {
                GameManager.Instance.RunTask(() => RequestData(output, null));
            }
        }

        private void RequestData(WriteStorageData output, byte[] mesh) {
            var update = new ChunkCacheUpdate();
            update.chunkEntryId = Region.GetId(Lod, Pos - Pos.GetChunkIndex(Region.MaxLod));
            update.version = CurrentVersion;
            
            if (mesh != null && MeshVersion == CurrentVersion) {
                update.meshData = mesh;
            }

            if (Soil != null) {
                update.soilDenData = Soil.ExportDensity();
                update.soilMatData = Soil.ExportMaterial();
            }
            
            output.PutData(Lod, Pos, update);
        }
    }
}