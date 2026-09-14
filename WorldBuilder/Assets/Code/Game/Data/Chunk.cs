using System;
using Game.GraphicGenerator;
using UnityEngine.Rendering;

namespace Game.Data {
    public class Chunk {
        
        public IndexPos Pos { get; }
        public int Lod { get; }
        public int EntryId { get; }
        
        private long version;
        public long Version {
            get {
                lock (key) {
                    return version;
                }
            }
        }
        private long currentVersion;
        public long CurrentVersion {
            get {
                lock (key) {
                    return currentVersion;
                }
            }
        }

        private long meshVersion;
        public long MeshVersion {
            get {
                lock (key) {
                    return meshVersion;
                }
            }
        }

        private long currentMeshVersion;
        public long CurrentMeshVersion {
            get {
                lock (key) {
                    return currentMeshVersion;
                }
            }
        }
        public MeshInterface SoilMesh { get; private set; }
        
        private long soilVersion;
        public long SoilVersion {
            get {
                lock (key) {
                    return soilVersion;
                }
            }
        }
        private long currentSoilVersion;
        public long CurrentSoilVersion {
            get {
                lock (key) {
                    return currentSoilVersion;
                }
            }
        }
        public ChunkSoil Soil { get; }

        public bool Modified {
            get {
                lock (key) {
                    return version != currentVersion || 
                           meshVersion != currentMeshVersion ||
                           soilVersion != currentSoilVersion;
                }
            }
        }
        
        public bool Released { get; set; } = true;
        
        public byte[] SoilMeshTempData { get; private set; }
        
        private readonly object key = new();

        private Chunk(IndexPos pos, int lod) {
            Pos = pos;
            Lod = lod;
            EntryId = Region.GetEntryId(Lod, Pos - Pos.GetChunkIndex(Region.MaxLod));
        }
        
        public Chunk(IndexPos pos, int lod, ChunkSoil soil, long ver) : this(pos, lod) {
            Soil = soil;
            currentVersion = ver;
            currentSoilVersion = ver;
        }

        public Chunk(IndexPos pos, int lod, ChunkCacheUpdate cache) : this(pos, lod) {
            Soil = new ChunkSoil(cache.soilDenData, cache.soilMatData);
            
            currentVersion = cache.version;
            version = CurrentVersion;
            
            SoilMeshTempData = cache.meshData;
            meshVersion = CurrentMeshVersion;
            
            currentSoilVersion = cache.soilVersion;
            soilVersion = CurrentSoilVersion;
        }

        /**
         * It can be called from any Thread
         */
        public void UpdateVersion(ChunkCacheUpdate update) {
            lock (key) {
                version = update.version;
                meshVersion = update.meshVersion;
                soilVersion = update.soilVersion;
            }
        }

        public bool CanCreateMesh() {
            return SoilMeshTempData != null;
        }

        public bool CreateMesh() {
            if (SoilMeshTempData != null) {
                try {
                    SoilMesh = new MeshInterface(Pos, Lod, SoilMeshTempData);
                    SoilMesh.AddReference();
                } catch {
                    return false;
                } finally {
                    SoilMeshTempData = null;
                }
                
                return true;
            }
            return false;
        }

        // public List<Structure> structures; // A structure has a chunkData special number [15] for LOD > 0
        // public List<Prop> props;           // Props are interactive objects, not visible on LOD > 0
        // public List<Item> items;           // Temporary collectables items
        // public List<Unit> units;           // Any Mob, Boss, Npc and similar
        // public List<Tree> trees;           // Tree can be visible at any LOD
        // public List<Grass> grass;          // Grass uses a special rendering method

        public bool RequestMeshAsync(Action<long, byte[]> output, out AsyncGPUReadbackRequest ret) {
            if (SoilMesh != null) {
                long ver = CurrentMeshVersion;
                
                if (SoilMesh.IsEmpty) {
                    output.Invoke(ver, new byte[1]);
                    ret = default;
                    return false;
                }
                
                ret = SoilMesh.RequestDataAsync(data => {
                    if (data == null) {
                        output.Invoke(0, null);
                    } else {
                        output.Invoke(ver, data);
                    }
                });
                return true;
            } else {
                output.Invoke(CurrentMeshVersion, SoilMeshTempData);
                ret = default;
                return false;
            }
        }

        /**
         * It can be called from any Thread
         */
        public void ExportData(ChunkCacheUpdate update) {
            lock (key) {
                update.version = currentVersion;
                update.soilVersion = currentSoilVersion;
                update.soilDenData = Soil.ExportDensity();
                update.soilMatData = Soil.ExportMaterial();
            }
        }

        public void EditChunkSoil(long ver) {
            lock (key) {
                // Soil.ApplyMod...
                currentSoilVersion = ver;
            }
        }

        public void EditChunkList(long ver) {
            lock (key) {
                currentVersion = ver;
            }
        }

        public void EditChunkMesh(long ver, MeshInterface mesh) {
            lock (key) {
                SoilMesh?.RemoveReference();
                SoilMesh = mesh;
                SoilMesh.AddReference();
                currentMeshVersion = ver;
            }
        }
    }
}