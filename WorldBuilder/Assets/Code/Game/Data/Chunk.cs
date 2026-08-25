using Code.Data;
using Code.Worlds.Storage;
using UnityEngine;

namespace Game.Data {
    public class Chunk {
        
        public IndexPos Pos { get; private set; }
        
        public int Lod { get; private set; }
        public int Version { get; set; }
        public int CurrentVersion { get; private set; }
        public ChunkSoil Soil { get; private set; }
        public Mesh Mesh { get; private set; }
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
            
        }
        
        public ChunkCacheUpdate Export() {
            return null;
        }
        
        public byte[] ExportSoil() {
            return null;
        }

        public byte[] ExportMesh() {
            return null;
        }

        public byte[] ExportList() {
            return null;
        }
    }
}