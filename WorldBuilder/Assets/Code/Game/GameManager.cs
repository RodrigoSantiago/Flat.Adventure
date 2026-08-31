using System;
using System.Collections.Generic;
using System.Threading;
using Code;
using Game.Data;
using Game.GraphicGenerator;
using Game.Worlds;
using UnityEngine;

namespace Game {
    public class GameManager : MonoBehaviour {
        public static GameManager Instance { get; private set; }
        
        private long LoopTick => 0;
        private readonly int GameSpeed = 60;

        private Thread mainThread;
        private List<Action> syncQueue = new();
        private List<Action> tempSyncQueue = new();
        private readonly Queue<Action> taskQueue = new();

        [SerializeField] private ChunkMeshGenerator meshGenerator;
        
        private WorldManager overWorld;

        public void Awake() {
            Instance = this;
            mainThread = Thread.CurrentThread;
        }

        public void Start() {
            // Load Game
            // Setup WorldManagers
            overWorld = new WorldManager(Application.persistentDataPath + "/OverWorld");
            
            for (int x = 0; x <= 2; x++) 
            for (int y = 0; y <= 2; y++)
            for (int z = 0; z <= 2; z++) {
                overWorld.Builder.RequestChunk(new IndexPos(x, y, z), 0);
            }
        }

        public void Update() {
            ExecuteSyncQueue();
            ExecuteTaskQueue();
        }

        public void OnDestroy() {
            
        }

        public void RequestMesh(Chunk[] chunk, Action<Mesh> action) {
            meshGenerator.Remesh(chunk, action);
        }

        public void RunSync(Action action) {
            if (Thread.CurrentThread == mainThread) {
                action.Invoke();
            } else {
                lock (syncQueue) {
                    syncQueue.Add(action);
                }
            }
        }

        public void RunTask(Action action) {
            lock (taskQueue) {
                taskQueue.Enqueue(action);
            }
        }

        private void ExecuteTaskQueue() {
            int maxTime = 1000 / GameSpeed;
            
            DateTime start = DateTime.Now;
            
            Action action;
            lock (taskQueue) {
                taskQueue.TryDequeue(out action);
            }

            while (action != null) {
                action.Invoke();
                
                var period = DateTime.Now - start;
                if (period.Milliseconds > maxTime) {
                    break;
                }
                
                lock (taskQueue) {
                    taskQueue.TryDequeue(out action);
                }
            }
        }

        private void ExecuteSyncQueue() {
            lock (syncQueue) {
                (syncQueue, tempSyncQueue) = (tempSyncQueue, syncQueue);
            }

            foreach (var action in tempSyncQueue) {
                action.Invoke();
            }
            tempSyncQueue.Clear();
        }
    }
}