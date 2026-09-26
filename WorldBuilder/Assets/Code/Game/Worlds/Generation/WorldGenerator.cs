using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Data;
using Game.Data.Queues;
using Game.Worlds.Exceptions;
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
        const int CHUNK_SIZE = CHUNK_SIZE_DEN + CHUNK_SIZE_MAT;
        
        [SerializeField] private ComputeShader shader;
        private ComputeBuffer voxelBuffer;
        private int generate;
        private int mipmaps;
        
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
            mipmaps = shader.FindKernel("Mipmaps");
            
            int voxels = 585 * CHUNK_SIZE;
            voxelBuffer = new ComputeBuffer(voxels / sizeof(uint), sizeof(uint), ComputeBufferType.Raw);
        }

        public void OnDestroy() {
            voxelBuffer.Dispose();
            dispose = true;
        }

        public void Update() {
            consumer.Consume();
        }

        private List<Chunk> requestChunks = new();
        
        private void Generate(Work work) {
            requestChunks.Clear();
            
            Request(work, 0);
        }

        private void Request(Work work, int piece) {

            if (piece < 8) {
                shader.SetBuffer(generate, VoxelBuffer, voxelBuffer);
                shader.SetVector(ChunkPos, (Vector3)work.Pos);
                shader.SetVector(IndexPos, new Vector4(0, piece * 32, 0, 0));
                shader.Dispatch(generate, 8 * 4, 4, 8 * 4);
                AsyncGPUReadback.Request(voxelBuffer, 
                    CHUNK_SIZE * (64), 
                    CHUNK_SIZE * (64 * piece), 
                    request => { OnRequestDone(work, request, piece); });
            } else {
                shader.SetBuffer(mipmaps, VoxelBuffer, voxelBuffer);
                shader.SetVector(IndexPos, new Vector4(0, 0, 0, 1));
                shader.Dispatch(mipmaps, 64 * 4, 4, 4);
                shader.SetVector(IndexPos, new Vector4(0, 0, 0, 2));
                shader.Dispatch(mipmaps,  8 * 4, 4, 4);
                shader.SetVector(IndexPos, new Vector4(0, 0, 0, 3));
                shader.Dispatch(mipmaps,  1 * 4, 4, 4);
                AsyncGPUReadback.Request(voxelBuffer, 
                    CHUNK_SIZE * (64 + 8 + 1), 
                    CHUNK_SIZE * (64 * piece), 
                    request => { OnRequestDone(work, request, piece); });
            }
        }

        private void OnRequestDone(Work work, AsyncGPUReadbackRequest request, int piece) {
            if (dispose) {
                requestChunks.Clear();
                work.SetError(new CacheCancelled());
                return;
            }
            
            if (request.hasError) {
                requestChunks.Clear();
                work.SetError(new Exception("Failed to generate"));
                return;
            }

            try {
                var data = request.GetData<byte>();
                int chunkOffset = piece * 64;
                int count = piece < 8 ? 64 : 64 + 8 + 1;
                
                Parallel.For(0, count, i => {
                    int offset = i * CHUNK_SIZE;
                    var den = GetPiece(data, offset, CHUNK_SIZE_DEN).ToArray();
                    var mat = GetPiece(data, offset + CHUNK_SIZE_DEN, CHUNK_SIZE_MAT).ToArray();
                    den[0] = 3;
                    mat[0] = 4;

                    int chunkIndex = chunkOffset + i;
                    int lod = Region.GetLod(chunkIndex);
                    var pos = Region.GetLocalPosition(lod, Region.GetLocalGroupId(chunkIndex));
                    var soil = new ChunkSoil(den, mat);
                    var chunk = new Chunk(work.Pos + pos, lod, soil, 1);
                    
                    lock (requestChunks) {
                        requestChunks.Add(chunk);
                    }
                });
                
                if (piece < 8) {
                    Request(work, piece + 1);
                } else {
                    work.Manager.Cache.PutRegion(work.Pos, requestChunks);
                    requestChunks.Clear();
                    work.SetSuccess();
                }
            } catch (Exception e) {
                requestChunks.Clear();
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