using System;
using System.Collections.Generic;

namespace Adventure.Logic {
    
    /**
     * World class is a middle line between the 
     */
    public class World {

        public int Seed { get; protected set; }
        public int Width { get; protected set; }
        public int Height { get; protected set; }
        public int Depth { get; protected set; }
        protected Noise noise;
        protected WorldMap worldMap;
        protected Dictionary<Point3, Chunk> chunks = new Dictionary<Point3, Chunk>();
        
        // - Cache X/Y Biome
        // - Cache Clean Lines
        // - Cache Joined Lines

        protected World(int seed) {
            Seed = seed;
            noise = new Noise(seed);
        }
        
        public World(int seed, int width, int height, int depth) {
            Seed = seed;
            Width = width;
            Height = height;
            Depth = depth;
            noise = new Noise(seed);
            worldMap = new WorldMap(this, noise);
        }

        public Chunk LoadChunk(Point3 local) {
            Chunk chunk;
            if (chunks.TryGetValue(local, out chunk)) {
                return chunk;
            } else {
                chunk = worldMap.GenerateChunk(local);
                chunks.Add(local, chunk);
            }

            return chunk;
        }

        public void UnloadChunk(Chunk chunk) {
            if (!chunk.IsIntact) {
                // save to file
            }
            
            chunks.Remove(chunk.Local);
        }

        public void FindCommonPalette(int id) {
            
        }

        public Palette LoadPalette(short[] readTypes, int usedTypes) {
            return null;
        }

        private void mixLines(BlockLine lineA, BlockLine lineB, BlockLine line) {
            
        }
        
        private void mixLines(BlockLine lineA, BlockLine lineB, BlockLine lineC, BlockLine line) {
            
        }
    }
}
