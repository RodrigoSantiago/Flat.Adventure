using System;
using Adventure.Game.Controller;
using Adventure.Game.Manager;
using Adventure.Logic;
using UnityEngine;

namespace Adventure.Game {
    public class GameServer : MonoBehaviour {

        public ChunkManager chunkManager;
        public UnitManager unitManager;

        private LocalController localController;
        private World world;

        private void Start() {
            CreateWorld();
            
            ConnectToWorld();
        }

        private void Update() {
            world?.Update(Time.deltaTime);
        }

        public void CreateWorld() {
            world = new World(1234, 16, 16, 16);
        }

        public void ConnectToWorld() {
            localController = new LocalController("Test", chunkManager, unitManager);
            world.AddController(localController);
        }
    }
}