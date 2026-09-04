using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Code;
using Game.Data;
using Game.Entities.Player;
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
        public WorldManager OverWorld => overWorld;

        public void Awake() {
            Instance = this;
            mainThread = Thread.CurrentThread;
        }

        public void Start() {
            // Load GameData
            GameData.Instance.players.Add(0, new PlayerData());
            
            // Setup WorldManagers
            overWorld = new WorldManager(Application.persistentDataPath + "/OverWorld");
            overWorld.SetViewPoint(new IndexPos(0, 0, 0));
            
            // Setup Player
            foreach (var player in GameData.Instance.players) {
                var cvPlayer = new GameObject("Player").AddComponent<CvPlayerUnit>().Setup();
                cvPlayer.transform.position = new Vector3(1024, 1024, 1024);
                PlayerSetView(cvPlayer);
            }
        }

        public void Update() {
            overWorld.RequestChunks();
            ExecuteSyncQueue();
            ExecuteTaskQueue();
        }

        public void OnDestroy() {
            overWorld.Dispose();
        }

        public void PlayerSetView(CvPlayerUnit cvPlayerUnit) {
            overWorld.SetViewPoint((IndexPos)cvPlayerUnit.transform.position);
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