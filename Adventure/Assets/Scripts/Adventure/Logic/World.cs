using System;
using System.Collections;
using System.Collections.Generic;
using Adventure.Logic.Data;
using Adventure.Logic.Generator;
using UnityEngine;

namespace Adventure.Logic {
    
    /**
     * World class is a middle line between the 
     */
    public class World {

        public long seed { get; }

        public readonly WorldSettings settings;
        
        public WorldMap worldMap { get; private set; }
        public bool alive { get; private set; }
        
        protected readonly Noise noise;
        protected readonly Dictionary<string, WorldPlayerController> controllers = new();

        private readonly WorldChunkController chunkController;
        private readonly WorldGenerator worldGenerator;

        public World(long seed, int width, int height, int length) {
            this.alive = true;
            
            this.seed = seed;
            this.settings = new WorldSettings(width, height, length);
            
            noise = new Noise(seed);
            worldMap = new WorldMap(this, noise);
            worldGenerator = new WorldGenerator(this, worldMap);
            chunkController = new WorldChunkController(this, worldGenerator);
        }

        public void Close() {
            alive = false;
        }

        public IEnumerator GenerateWorldMap() {
            yield break;
        }

        public void AddController(WorldPlayerController controller) {
            controller.player = new WorldPlayer(controller, controller.name);
            controller.player.world = this;
            controllers[controller.name] = controller;
            controller.player.Spawn();
        }

        public void RemoveController(WorldPlayerController controller) {
            controller.player.Vanish();
            controllers.Remove(controller.name);
        }

        public void Update(float delta) {
            WorldTime.deltaTime = delta;

            chunkController.LoadChunkSchedule();
            PlayerUpdate();
        }

        public void PlayerUpdate() {
            var keys = controllers.Keys;
            foreach (var key in keys) {
                if (controllers.TryGetValue(key, out var controller)) {
                    controller.player.Update();
                }
            }
        }

        public void RequestChunk(Vector3Int local, WorldPlayerController controller = null, int lod = 0) {
            chunkController.RequestChunk(local, controller, lod);
        }

        public bool IsChunkReady(Vector3Int local) {
            return chunkController.IsChunkReady(local);
        }
        
    }
}
