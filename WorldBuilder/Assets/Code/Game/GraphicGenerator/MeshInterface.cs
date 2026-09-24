using System;
using System.Runtime.InteropServices;
using Game.Data;
using Game.Utils;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.GraphicGenerator {
    public class MeshInterface {
        private static readonly int BufferVertexBuffer = Shader.PropertyToID("BufferVertexBuffer");
        private static readonly int MeshVertexBuffer = Shader.PropertyToID("MeshVertexBuffer");
        private static readonly int MeshIndexBuffer = Shader.PropertyToID("MeshIndexBuffer");
        private static readonly int VertexCount = Shader.PropertyToID("vertex_count");
        private static readonly int ChunkPos = Shader.PropertyToID("ChunkPos");
        private static readonly int BufferOffset = Shader.PropertyToID("BufferOffset");
        
        private const int BufferStride = 16;
        private const int MeshVertexStride = 32;
        
        public static bool Native { get; set; } = true;
        
        public Mesh Mesh { get; private set; }
        public bool IsEmpty { get; private set; }
        
        private MaterialPropertyBlock propertyBlock;
        private Allocation<GraphicsBuffer> allocation;
        private int elements;

        private readonly IndexPos pos;
        private readonly int lod;
        private int reference;

        public MeshInterface(IndexPos pos, int lod) {
            this.pos = pos;
            this.lod = lod;
        }

        public MeshInterface(IndexPos pos, int lod, byte[] data) {
            this.pos = pos;
            this.lod = lod;
            Recreate(data);
        }

        public void Compose(ComputeShader shader, int kernelMesh, int kernelBuffer, int vertexCount, int index) {
            if (vertexCount == 0) {
                IsEmpty = true;
                return;
            }
            
            if (Native) {
                ComposeBuffer(shader, kernelBuffer, vertexCount, index);
            } else {
                ComposeMesh(shader, kernelMesh, vertexCount, index);
            }
        }

        public void AddReference() {
            reference++;
        }
        
        public void RemoveReference() {
            reference--;
            if (reference <= 0 && !IsEmpty) {
                GameManager.Instance.RunTask(Dispose);
            }
        }
        
        public void Dispose() {
            if (allocation != null) {
                GraphicsBufferAllocator.Allocator.Free(allocation);
                allocation = null;
            }
            
            if (Mesh != null) {
                UnityEngine.Object.Destroy(Mesh);
                Mesh = null;
            }

            propertyBlock = null;
        }
        
        private void ComposeBuffer(ComputeShader shader, int kernel, int vertexCount, int index) {
            elements = vertexCount;
            allocation = GraphicsBufferAllocator.Allocator.Alloc(elements);
            
            propertyBlock = new MaterialPropertyBlock();
            propertyBlock.SetBuffer(MeshVertexBuffer, allocation.Source);
            propertyBlock.SetVector(ChunkPos, new Vector4(pos.x, pos.y, pos.z, 1 << lod));
            propertyBlock.SetInt(BufferOffset, allocation.Index);

            shader.SetBuffer(kernel, BufferVertexBuffer, allocation.Source);
            shader.SetInt(BufferOffset, allocation.Index);

            shader.SetInts(VertexCount, vertexCount, 1, 0, index);
            shader.Dispatch(kernel, Mathf.CeilToInt(vertexCount / 64f), 1, 1);
        }

        private void ComposeMesh(ComputeShader shader, int kernel, int vertexCount, int index) {
            elements = vertexCount;
            
            Mesh = new Mesh();
            Mesh.indexFormat = IndexFormat.UInt32;
            Mesh.indexBufferTarget |= GraphicsBuffer.Target.Structured;
            Mesh.SetVertexBufferParams(vertexCount,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float16, 4),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float16, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float16, 4)
            );
            Mesh.SetIndexBufferParams(vertexCount, IndexFormat.UInt32);
            Mesh.subMeshCount = 1;
            Mesh.SetSubMesh(0, new SubMeshDescriptor(0, vertexCount), MeshUpdateFlags.DontRecalculateBounds);
            Mesh.vertexBufferTarget = GraphicsBuffer.Target.Structured;

            Mesh.bounds = new Bounds(new Vector3(16, 16, 16), new Vector3(32, 32, 32));

            var meshVertex = Mesh.GetVertexBuffer(0);
            var meshIndex = Mesh.GetIndexBuffer();

            try {
                shader.SetBuffer(kernel, MeshVertexBuffer, meshVertex);
                shader.SetBuffer(kernel, MeshIndexBuffer, meshIndex);

                shader.SetInts(VertexCount, vertexCount, 0, 0, index);
                shader.Dispatch(kernel, Mathf.CeilToInt(vertexCount / 64f), 1, 1);
				
            } finally {
                meshVertex.Dispose();
                meshIndex.Dispose();
            }
        }
        
        public void Recreate(byte[] data) {
            if (data == null || data.Length == 0) return;

            Dispose();

            if (data.Length == 1) {
                IsEmpty = true;
            } else if (Native) {
                RecreateBuffer(data);
            } else {
                RecreateMesh(data);
            }
        }
        
        private void RecreateBuffer(byte[] data) {
            if (data.Length % BufferStride != 0)
                throw new ArgumentException($"Invalid buffer data size: {data.Length}");

            elements = data.Length / BufferStride;
            allocation = GraphicsBufferAllocator.Allocator.Alloc(elements);
            
            var values = MemoryMarshal.Cast<byte, uint4>(data).ToArray();
            allocation.Source.SetData(
                values,
                0, 
                allocation.Index,
                allocation.Count
            );

            propertyBlock = new MaterialPropertyBlock();
            propertyBlock.SetBuffer(MeshVertexBuffer, allocation.Source);
            propertyBlock.SetVector(ChunkPos, new Vector4(pos.x, pos.y, pos.z, 1 << lod));
            propertyBlock.SetInt(BufferOffset, allocation.Index);
        }

        private void RecreateMesh(byte[] data) {
            if (data.Length % MeshVertexStride != 0)
                throw new ArgumentException($"Invalid mesh data size: {data.Length}. ");

            int vertexCount = data.Length / MeshVertexStride;

            elements = vertexCount;

            Mesh = new Mesh {
                indexFormat = IndexFormat.UInt32,
                vertexBufferTarget = GraphicsBuffer.Target.Structured,
                bounds = new Bounds(new Vector3(16, 16, 16), new Vector3(32, 32, 32))
            };

            Mesh.SetVertexBufferParams(vertexCount,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float16, 4),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float16, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float16, 4)
            );

            Mesh.SetIndexBufferParams(vertexCount, IndexFormat.UInt32);

            Mesh.SetVertexBufferData(data, 0, 0, data.Length, 0, MeshUpdateFlags.DontRecalculateBounds);

            var indices = new NativeArray<uint>(vertexCount, Allocator.Temp);

            try {
                for (uint i = 0; i < vertexCount; i++)
                    indices[(int)i] = i;

                Mesh.SetIndexBufferData(indices, 0, 0, vertexCount, MeshUpdateFlags.DontRecalculateBounds);
            } finally {
                indices.Dispose();
            }

            Mesh.subMeshCount = 1;
            Mesh.SetSubMesh(0, 
                new SubMeshDescriptor(0, vertexCount) { topology = MeshTopology.Triangles }, 
                MeshUpdateFlags.DontRecalculateBounds);
        }

        public void Render(MeshRenderBuffer renderBuffer) {
            if (IsEmpty) return;
            
            if (Native) {
                RenderBuffer(renderBuffer);
            } else {
                RenderMesh(renderBuffer);
            }
        }
        
        private void RenderBuffer(MeshRenderBuffer renderBuffer) {
            if (allocation == null) return;

            /*var renderParams = renderBuffer.Params;
            renderParams.matProps = propertyBlock;
            renderParams.worldBounds = new Bounds(
                new Vector3(16, 16, 16) * (1 << lod) + (Vector3)pos,
                new Vector3(32, 32, 32) * (1 << lod)
            );

            Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, elements);*/
            renderBuffer.Add(pos, lod, allocation.Source, allocation.Index, allocation.Count);
        }
        
        private void RenderMesh(MeshRenderBuffer renderBuffer) {
            if (Mesh == null) return;
            
            var matrix = Matrix4x4.TRS((Vector3)pos, Quaternion.identity, Vector3.one * (1 << lod));
            Graphics.RenderMesh(renderBuffer.Params, Mesh, 0, matrix);
        }

        public AsyncGPUReadbackRequest RequestDataAsync(Action<byte[]> action) {
            if (Native) {
                return RequestBufferDataAsync(action);
            } else {
                return RequestMeshDataAsync(action);
            }
        }

        private AsyncGPUReadbackRequest RequestBufferDataAsync(Action<byte[]> action) {
            return AsyncGPUReadback.Request(allocation.Source, 
                allocation.Count * BufferStride, 
                allocation.Index * BufferStride, request => {
                    if (request.hasError) {
                        action.Invoke(null);
                        return;
                    }

                    var vertices = request.GetData<Vector4>();
                        
                    byte[] bytes = vertices.Reinterpret<byte>(Marshal.SizeOf<Vector4>()).ToArray();
                        
                    action.Invoke(bytes);
                });
        }

        private AsyncGPUReadbackRequest RequestMeshDataAsync(Action<byte[]> action) {
            var gBuffer = Mesh.GetVertexBuffer(0);

            return AsyncGPUReadback.Request(gBuffer, request => {
                if (request.hasError) {
                    gBuffer.Dispose();
                    action.Invoke(null);
                    return;
                }

                var vertices = request.GetData<GeneratedVertexLow>();
                    
                byte[] bytes = vertices.Reinterpret<byte>(Marshal.SizeOf<GeneratedVertexLow>()).ToArray();
                gBuffer.Dispose();
                    
                action.Invoke(bytes);
            });
        }

        public byte[] RequestData() {
            if (IsEmpty) return new byte[1];
            
            if (Native) {
                return RequestBufferData();
            } else {
                return RequestMeshData();
            }
        }
        
        private byte[] RequestMeshData() {
            if (Mesh == null) return null;

            byte[] data = new byte[elements * MeshVertexStride];
            
            var gBuffer = Mesh.GetVertexBuffer(0);
            try {
                gBuffer.GetData(data);
            } finally {
                gBuffer.Dispose();
            }

            return data;
        }
        
        private byte[] RequestBufferData() {
            if (allocation == null) return null;

            byte[] data = new byte[elements * BufferStride];
            allocation.Source.GetData(data, 0, allocation.Index * BufferStride, allocation.Count * BufferStride);

            return data;
        }
    }
}