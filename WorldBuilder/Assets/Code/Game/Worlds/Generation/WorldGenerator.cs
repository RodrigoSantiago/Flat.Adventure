using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Data;
using Game.Data.Queues;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Worlds.Generation {
    public class WorldGenerator : MonoBehaviour {
        public static WorldGenerator Instance { get; private set; }
        
        private class Work : ITaskGroup<Work> {
            
            public IndexPos Pos { get; }
            private int compare;

            public WorldGenerator Generator { get; }
            public WorldManager Manager { get; }
            
            public int Attempts { get; set; }
            public bool IsCompleted { get; private set; }
            private readonly List<TaskListener> listeners;

            public Work(WorldGenerator generator, WorldManager manager, IndexPos pos, TaskListener listener) {
                Generator = generator;
                Manager = manager;
                Pos = pos;
                listeners = new List<TaskListener> { listener };
            }

            public void SetSortValue(IndexPos sort) {
                var d = Pos - sort;
                compare = d.x * d.x + d.y * d.y + d.z * d.z;
            }

            public int Compare() {
                return compare;
            }

            public bool Group(Work task) {
                if (task.Pos == Pos) {
                    listeners.AddRange(task.listeners);
                    return true;
                }

                return false;
            }

            public void Execute() {
                Generator.Generate(this);
            }
            
            public void SetSuccess() {
                IsCompleted = true;
                foreach (var listener in listeners) {
                    listener.OnSuccess?.Invoke();
                }
            }

            public void SetError(Exception message) {
                IsCompleted = true;
                foreach (var listener in listeners) {
                    listener.OnError?.Invoke(message);
                }
            }
        }


        const int CHUNK_SIZE_DEN = 16388;
        const int CHUNK_SIZE_MAT = 24580;
        const int MAT_OFFSET = (585 * CHUNK_SIZE_DEN);
        
        [SerializeField] private ComputeShader shader;
        private ComputeBuffer voxelBuffer;
        private int generate;
        
        private ManualConsumer<Work> consumer;
        private bool dispose;
        private static readonly int VoxelBuffer = Shader.PropertyToID("VoxelBuffer");
        private static readonly int ChunkPos = Shader.PropertyToID("ChunkPos");
        private static readonly int IndexPos = Shader.PropertyToID("IndexPos");

        private void Awake() {
            Instance = this;
            Init();
        }

        public void Init() {
            consumer = new ManualConsumer<Work>("World-Generator");
            
            shader = Resources.Load<ComputeShader>("WorldGen");
            generate = shader.FindKernel("Generate");
            
            int voxels = 585 * CHUNK_SIZE_DEN + 585 * CHUNK_SIZE_MAT;
            voxelBuffer = new ComputeBuffer(voxels / sizeof(uint), sizeof(uint), ComputeBufferType.Raw);
        }

        public void OnDestroy() {
            voxelBuffer.Dispose();
            dispose = true;
        }

        public void Update() {
            consumer.Consume();
        }
        
        private void Generate(Work work) {
            shader.SetBuffer(generate, VoxelBuffer, voxelBuffer);
            shader.SetVector(ChunkPos, (Vector3)work.Pos);

            var now = DateTime.Now;
            int size = 8 * 4;
            shader.Dispatch(generate, size, size, size);
            
            AsyncGPUReadback.Request(voxelBuffer, request => {
                var now2 = DateTime.Now;
                OnRequestDone(work, request);
                var now3 = DateTime.Now;
                Debug.Log("GPU: " + (now2 - now) + " -- CPU: " + (now3 - now2));
            });
        }

        private void OnRequestDone(Work work, AsyncGPUReadbackRequest request) {
            if (dispose) return;
                
            if (request.hasError) {
                work.SetError(new Exception("Failed to generate"));
                return;
            }

            try {
                var data = request.GetData<byte>();
                int dOffset = 0;
                int mOffset = MAT_OFFSET;

                for (int lod = 0; lod < Region.TotalLods; lod++) {
                    int count = Region.LodSize3[lod];
                    var chunks = new Chunk[count];

                    int lodDOffset = dOffset;
                    int lodMOffset = mOffset;

                    int localLod = lod;
                    Parallel.For(0, count, i => {
                        int offsetD = i * CHUNK_SIZE_DEN;
                        int offsetM = i * CHUNK_SIZE_MAT;
                        var den = GetPiece(data, lodDOffset + offsetD, CHUNK_SIZE_DEN).ToArray();
                        var mat = GetPiece(data, lodMOffset + offsetM, CHUNK_SIZE_MAT).ToArray();
                        den[0] = 3;
                        mat[0] = 4;
                        
                        var soil = new ChunkSoil(den, mat);
                        chunks[i] = new Chunk(work.Pos + Region.GetLocalPosition(localLod, i), localLod, soil, 1);
                    });

                    dOffset += count * CHUNK_SIZE_DEN;
                    mOffset += count * CHUNK_SIZE_MAT;

                    work.Manager.Cache.PutRegion(work.Pos, chunks);
                }

                work.SetSuccess();
            } catch (Exception e) {
                work.SetError(e);
            }
        }
        
        private ReadOnlySpan<byte> GetPiece(NativeArray<byte> data, int start, int length) {
            unsafe {
                return new ReadOnlySpan<byte>((byte*)data.GetUnsafeReadOnlyPtr() + start, length);
            }
        }

        public void SetPriorityCenter(IndexPos center) {
            consumer.SortValue = center;
        }

        public void EnqueueRegion(WorldManager manager, IndexPos regionIndex, Action onGenerated, Action<Exception> onError) {
            consumer.Enqueue(new Work(this, manager, regionIndex, new TaskListener(onGenerated, onError)));
        }
    }
}