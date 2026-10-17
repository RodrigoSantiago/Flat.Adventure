using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Game.Data;
using Game.Data.Queues;
using Game.Worlds.Exceptions;
using K4os.Compression.LZ4;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Worlds.Generation {
    public class WorldMapGenerator : MonoBehaviour {
        public static WorldMapGenerator Instance { get; private set; }
        
        private class Work : ITaskGroup<Work> {
            
            public IndexPos Pos { get; }
            private int compare;

            public WorldMapGenerator MapGenerator { get; }
            public WorldManager Manager { get; }
            
            public int Attempts { get; set; }
            public bool IsCompleted { get; private set; }
            private bool IsFullRequest { get; }
            public bool IsCancelled { get; private set; }
            public ProgressToken Progress { get; }
            private readonly List<TaskListener<byte[]>> listeners;
            private byte[] data;
            
            public Work(WorldMapGenerator mapGenerator, WorldManager manager, ProgressToken progress, TaskListener<byte[]> listener) {
                MapGenerator = mapGenerator;
                Manager = manager;
                Pos = new IndexPos(0, 0, 0);
                Progress = progress;
                IsFullRequest = true;
                listeners = new List<TaskListener<byte[]>> { listener };
            }

            public Work(WorldMapGenerator mapGenerator, WorldManager manager, IndexPos pos, TaskListener<byte[]> listener) {
                MapGenerator = mapGenerator;
                Manager = manager;
                Pos = pos;
                IsFullRequest = false;
                listeners = new List<TaskListener<byte[]>> { listener };
            }

            public void SetSortValue(IndexPos sort) {
                var d = Pos - sort;
                compare = d.x * d.x + d.y * d.y + d.z * d.z;
            }

            public int Compare() {
                return compare;
            }

            public bool Group(Work task) {
                if (!IsFullRequest && task.Manager == Manager && task.Pos == Pos) {
                    listeners.AddRange(task.listeners);
                    return true;
                }

                return false;
            }

            public void Execute() {
                if (IsFullRequest) {
                    MapGenerator.FullRequest(this);
                } else {
                    MapGenerator.SingleRequest(this);
                }
            }

            public void SetGenerated(byte[] generatedData) {
                this.data = generatedData;
                SetSuccess();
            }
            
            public void SetSuccess() {
                IsCompleted = true;
                foreach (var listener in listeners) {
                    listener.OnSuccess?.Invoke(data);
                }
            }

            public void SetError(Exception message) {
                IsCompleted = true;
                foreach (var listener in listeners) {
                    listener.OnError?.Invoke(message);
                }
            }

            public void RequestCancel() {
                IsCancelled = true;
            }
        }

        private ManualConsumer<Work> consumer;
        private ComputeShader shader;
        private int kernelGenerate;
        
        private ComputeBuffer heightBuffer;
        private ComputeBuffer mapBuffer;
        private bool dispose;
        private static readonly int MapBuffer = Shader.PropertyToID("MapBuffer");
        private static readonly int HeightBuffer = Shader.PropertyToID("HeightBuffer");
        private static readonly int PiecePos = Shader.PropertyToID("PiecePos");

        private byte[] emptyData;

        private void Awake() {
            Instance = this;
            Init();
        }

        public void Init() {
            consumer = new ManualConsumer<Work>("World-Map-Generator");
            
            shader = Resources.Load<ComputeShader>("WorldMapGen");
            kernelGenerate = shader.FindKernel("Generate");
            
            int maxSize = 1024 * 8 * 1024 * 16;
            int heightSizeBytes = (maxSize / 256 / 256) * 2;
            heightBuffer = new ComputeBuffer(heightSizeBytes / sizeof(uint), sizeof(uint), ComputeBufferType.Raw);
            emptyData = new byte[heightSizeBytes];

            int pieceSizeBytes = (1024 * 1024) * 4;
            mapBuffer = new ComputeBuffer(pieceSizeBytes / sizeof(uint), sizeof(uint), ComputeBufferType.Raw);
        }

        public void OnDestroy() {
            dispose = true;
            heightBuffer?.Dispose();
            mapBuffer?.Dispose();
        }

        public void Update() {
            consumer.Consume();
        }

        public void Cancel(WorldManager manager) {
            consumer.CancelWhere(w => w.Manager == manager);
        }

        private void FullRequest(Work work) {
            string path = work.Manager.Storage + "/WorldHeightMap.data";
            if (File.Exists(path)) {
                GameManager.Instance.TaskListener(ReadAsync(path, default), work.SetGenerated, (e) => {
                    GenerateOrderedRequest(work, 0);
                });
            } else {
                GenerateOrderedRequest(work, 0);
            }
        }

        private void GenerateOrderedRequest(Work work, int id) {
            if (dispose || work.IsCancelled) {
                work.SetError(new CacheCancelled());
                return;
            }

            if (id == 0) {
                heightBuffer.SetData(emptyData, 0, 0, emptyData.Length);
            }
            
            int w = work.Manager.Data.width / 1024;
            int h = work.Manager.Data.height / 1024;
            int total = w * h;
            var pos = new IndexPos(id % w * 1024, 0, id / w * 1024);
            work.Progress.Advance(1f / total);
            
            shader.SetBuffer(kernelGenerate, MapBuffer, mapBuffer);
            shader.SetBuffer(kernelGenerate, HeightBuffer, heightBuffer);
            shader.SetVector(PiecePos, new Vector4(pos.x, 0, pos.z, 0));
            shader.Dispatch(kernelGenerate, 1024 / 8, 1, 1024 / 8);
            AsyncGPUReadback.Request(mapBuffer, request => {
                if (dispose || work.IsCancelled) {
                    work.SetError(new CacheCancelled());
                    return;
                }
                if (request.hasError) {
                    work.SetError(new CacheGpuFailed(work.Manager.Name + ":FullMap"));
                    return;
                }
                
                var data = request.GetData<byte>().ToArray();
                string path = GetPathName(work, pos);
                GameManager.Instance.TaskListener(WriteAsync(path, data, default), () => {}, (e) => {});
                if (id < total - 1) {
                    GenerateOrderedRequest(work, id + 1);
                } else {
                    GenerateLastRequest(work);
                }
            });
        }

        private void GenerateLastRequest(Work work) {
            if (dispose || work.IsCancelled) {
                work.SetError(new CacheCancelled());
                return;
            }
            AsyncGPUReadback.Request(heightBuffer, request => {
                if (dispose || work.IsCancelled) {
                    work.SetError(new CacheCancelled());
                    return;
                }
                if (request.hasError) {
                    work.SetError(new CacheGpuFailed(work.Manager.Name + ":FullMap"));
                    return;
                }
                
                var data = request.GetData<byte>().ToArray();
                string path = work.Manager.Storage + "/WorldHeightMap.data";
                GameManager.Instance.TaskListener(WriteAsync(path, data, default), () => {
                    work.SetGenerated(data);
                }, work.SetError);
            });
        }

        private void SingleRequest(Work work) {
            string path = GetPathName(work, work.Pos);
            if (File.Exists(path)) {
                GameManager.Instance.TaskListener(ReadAsync(path, default), work.SetGenerated, (e) => {
                    GenerateSingleRequest(work);
                });
            } else {
                GenerateSingleRequest(work);
            }
        }

        private void GenerateSingleRequest(Work work) {
            if (dispose || work.IsCancelled) {
                work.SetError(new CacheCancelled());
                return;
            }
            
            shader.SetBuffer(kernelGenerate, MapBuffer, mapBuffer);
            shader.SetBuffer(kernelGenerate, HeightBuffer, heightBuffer);
            shader.SetVector(PiecePos, new Vector4(work.Pos.x, 0, work.Pos.z, 0));
            shader.Dispatch(kernelGenerate, 1024 / 8, 1, 1024 / 8);
            AsyncGPUReadback.Request(mapBuffer, request => {
                if (dispose || work.IsCancelled) {
                    work.SetError(new CacheCancelled());
                    return;
                }
                if (request.hasError) {
                    work.SetError(new CacheGpuFailed(work.Manager.Name + ":" + work.Pos));
                    return;
                }
                
                var data = request.GetData<byte>().ToArray();
                work.SetGenerated(data);
                
                string path = GetPathName(work, work.Pos);
                GameManager.Instance.TaskListener(WriteAsync(path, data, default), () => {}, (e) => {});
            });
        }

        private string GetPathName(Work work, IndexPos pos) {
            return work.Manager.Storage + "/WorldMapCache/Piece_" + Region.GenName(pos) + ".data";
        }

        public void Generate(WorldManager manager, ProgressToken progress, Action<byte[]> onSuccess, Action<Exception> onError) {
            consumer.Enqueue(new Work(this, manager, progress, new TaskListener<byte[]>(onSuccess, onError)));
        }

        public void GetBuffer(WorldManager manager, IndexPos region, Action<byte[]> onSuccess, Action<Exception> onError) {
            consumer.Enqueue(new Work(this, manager, region.GetChunkIndex(5), new TaskListener<byte[]>(onSuccess, onError)));
        }
        
        private static async Task<byte[]> ReadAsync(string path, CancellationToken token) {
            var compressedData = await File.ReadAllBytesAsync(path, token);
            return ReadAndDecompressBuffer(compressedData, compressedData.Length);
        }
        
        private static async Task WriteAsync(string path, byte[] worldMapData, CancellationToken token) {
            var (compressedBuffer, len) = CompressBuffer(worldMapData);

            await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await stream.WriteAsync(compressedBuffer, 0, len, token);
        }

        private static (byte[], int) CompressBuffer(byte[] rawData) {
            if (rawData == null || rawData.Length == 0) return (null, 0);

            int maxOutputSize = LZ4Codec.MaximumOutputSize(rawData.Length);
            byte[] compressedBuffer = new byte[sizeof(int) + maxOutputSize];

            compressedBuffer[0] = (byte)rawData.Length;
            compressedBuffer[1] = (byte)(rawData.Length >> 8);
            compressedBuffer[2] = (byte)(rawData.Length >> 16);
            compressedBuffer[3] = (byte)(rawData.Length >> 24);

            int encodedLength = LZ4Codec.Encode(
                rawData, 0, rawData.Length,
                compressedBuffer, sizeof(int), maxOutputSize,
                LZ4Level.L00_FAST
            );

            int totalCompressedSize = sizeof(int) + encodedLength;
            return (compressedBuffer, totalCompressedSize);
        }

        private static byte[] ReadAndDecompressBuffer(byte[] compressedData, int compressedLength) {
            int originalSize = compressedData[0]
                               | (compressedData[1] << 8)
                               | (compressedData[2] << 16)
                               | (compressedData[3] << 24);

            byte[] decompressedBuffer = new byte[originalSize];

            LZ4Codec.Decode(
                compressedData, sizeof(int), compressedLength - sizeof(int),
                decompressedBuffer, 0, originalSize
            );

            return decompressedBuffer;
        }
    }
}