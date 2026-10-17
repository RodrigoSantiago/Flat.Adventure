using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Data;
using Game.Entities.Player;
using Game.GraphicGenerator;
using Game.Worlds;
using Game.Worlds.Generation;
using UnityEngine;

namespace Game {
    public class GameManager : MonoBehaviour {
        public static GameManager Instance { get; private set; }

        private long LoopTick => 0;
        private readonly int GameSpeed = 60;

        private Thread mainThread;
        private List<Action> tempSyncQueue = new();
        private readonly Queue<Action> taskQueue = new();

        [SerializeField] private ChunkMeshGenerator meshGenerator;
        public ChunkMeshGenerator MeshGenerator => meshGenerator;

        [SerializeField] private WorldGenerator generator;
        public WorldGenerator WorldGenerator => generator;
        
        [SerializeField] private Camera playerCamera;
        public Camera PlayerCamera => playerCamera;

        private WorldManager overWorld;
        public WorldManager OverWorld => overWorld;

        public bool frustumOcclusion;
        public bool terrainShadow = true;
        public bool terrainColorOnly = true;
        public bool debugShadow = false;
        public bool debugRegions = false;
        [Range(0, 3)] public int maxLodView = 3;
        public Mesh smallCube;
        public Material smallCubeMat;
        
        private long hashVersion = 1;
        private long GetHashVersion => ++hashVersion;
        public bool DebugChunkPosition;

        public void Awake() {
            Instance = this;
            mainThread = Thread.CurrentThread;
        }

        public void Start() {
            // Load GameData
            GameData.Instance.players.Add(0, new PlayerData());
            
            // Setup WorldManagers
            overWorld = new WorldManager("OverWorld", Application.persistentDataPath + "/OverWorld");
            overWorld.Load();
        }

        public void Update() {
            ExecuteTaskQueue(1000 / GameSpeed);
            if (overWorld.IsLoading) {
                if (overWorld.LoadingError != null) {
                    // Show exception and Close
                    Debug.Log("Progress Error: " + overWorld.LoadingError);
                } else {
                    // Show progress bar
                    Debug.Log("Progress: " + overWorld.LoadingProgress);
                }
            }
            overWorld.Update();
        }

        public void OnDestroy() {
            overWorld.SaveAndExit();
            ExecuteTaskQueue(-1);
            GraphicsBufferAllocator.Allocator.Dispose();
        }

        public void PlayerSetView(CvPlayerUnit cvPlayerUnit) {
            overWorld.SetViewPoint((IndexPos)cvPlayerUnit.transform.position);
        }

        public void RunTask(Action action) {
            lock (taskQueue) {
                taskQueue.Enqueue(action);
            }
        }
        
        public async void TaskListener(Task task, Action onCompleted, Action<Exception> onError)  {
            try {
                await task;
                RunTask(onCompleted);
            } catch (OperationCanceledException) {
                RunTask(() => onError(null));
            } catch (Exception ex) {
                Debug.LogException(ex);
                RunTask(() => onError(ex));
            }
        }
        
        public async void TaskListener<T>(Task<T> task, Action<T> onCompleted, Action<Exception> onError) where T : class {
            try {
                var result = await task;
                RunTask(() => onCompleted(result));
            } catch (OperationCanceledException) {
                RunTask(() => onError(null));
            } catch (Exception ex) {
                Debug.LogException(ex);
                RunTask(() => onError(ex));
            }
        }

        private void ExecuteTaskQueue(long maxTime) {
            DateTime start = DateTime.Now;

            Action action;
            lock (taskQueue) {
                taskQueue.TryDequeue(out action);
            }

            while (action != null) {
                action.Invoke();

                var period = DateTime.Now - start;
                if (maxTime > -1 && period.Milliseconds > maxTime) {
                    break;
                }

                lock (taskQueue) {
                    taskQueue.TryDequeue(out action);
                }
            }
        }
    }

}