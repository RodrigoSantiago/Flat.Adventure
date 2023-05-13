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
        
        public int width { get; }
        
        public int height { get; }
        
        public int depth { get; }
        
        public WorldMap worldMap { get; private set; }
        
        protected Noise noise;
        protected readonly Dictionary<string, WorldPlayerController> controllers = new();

        public readonly WorldChunkController chunkController;

        public World(long seed, int width, int height, int depth) {
            this.seed = seed;
            this.width = width;
            this.height = height;
            this.depth = depth;
            noise = new Noise(seed);
            worldMap = new WorldMap(this, noise);

            this.chunkController = new WorldChunkController(this);
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

        public void RequestChunk(Vector3Int local, WorldPlayerController controller = null) {
            chunkController.RequestChunk(local, controller);
        }

        public bool IsChunkReady(Vector3Int local) {
            return chunkController.IsChunkReady(local);
        }

        public bool IsChunkForReady(Vector3Int local) {
            return chunkController.IsChunkForReady(local);
        }
        
    }
}
