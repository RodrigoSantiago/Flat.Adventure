using System;
using System.Collections;
using System.Collections.Generic;
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
        
        private static GameServer instance;

        private void Awake() {
            instance = this;
        }

        private void Start() {
            CreateWorld();
            
            ConnectToWorld();
        }

        private void Update() {
            world?.Update(Time.deltaTime);
        }

        public void CreateWorld() {
            world = new World(1234, 1024, 256, 1024);
        }

        public void ConnectToWorld() {
            localController = new LocalController("Test", chunkManager, unitManager);
            localController.world = world;
            chunkManager.controller = localController;
            chunkManager.settings = world.settings;
            
            world.AddController(localController);
        }

        public static Coroutine StartAsync(IEnumerator enumerator) {
            return instance.StartCoroutine(enumerator);
        }
    }
}