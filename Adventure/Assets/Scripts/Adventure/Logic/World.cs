using System;
using System.Collections.Generic;
using Adventure.Data;
using UnityEngine;

namespace Adventure.Logic {
    
    /**
     * World class is a middle line between the 
     */
    public class World {

        public long Seed { get; protected set; }
        public int Width { get; protected set; }
        public int Height { get; protected set; }
        public int Depth { get; protected set; }
        public WorldMap WorldMap { get; protected set; }
        protected Noise noise;
        protected Dictionary<Vector3Int, Chunk> chunks = new Dictionary<Vector3Int, Chunk>();
        
        // - Cache X/Y Biome
        // - Cache Clean Lines
        // - Cache Joined Lines

        protected World(long seed) {
            Seed = seed;
            noise = new Noise(seed);
        }
        
        public World(long seed, int width, int height, int depth) {
            Seed = seed;
            Width = width;
            Height = height;
            Depth = depth;
            noise = new Noise(seed);
            WorldMap = new WorldMap(this, noise);
        }

        public Chunk LoadChunk(Vector3Int local) {
            Chunk chunk;
            if (chunks.TryGetValue(local, out chunk)) {
                return chunk;
            } else {
                chunk = WorldMap.GenerateChunk(local);
                chunks.Add(local, chunk);
            }

            return chunk;
        }

        public void UnloadChunk(Chunk chunk) {
            if (!chunk.IsOriginal) {
                // save to file
            }
            
            chunks.Remove(chunk.Local);
        }

        public void FindCommonPalette(int id) {
            
        }

        private void mixLines(BlockLine lineA, BlockLine lineB, BlockLine line) {
            
        }
    }
}
