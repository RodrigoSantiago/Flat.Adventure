using System;
using System.Runtime.InteropServices;
using Game.Data;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.GraphicGenerator {
    public class MeshInterface {
        private static readonly int BufferVertexBuffer = Shader.PropertyToID("BufferVertexBuffer");
        private static readonly int MeshVertexBuffer = Shader.PropertyToID("MeshVertexBuffer");
        private static readonly int MeshIndexBuffer = Shader.PropertyToID("MeshIndexBuffer");
        private static readonly int VertexCount = Shader.PropertyToID("vertex_count");
        private static readonly int ChunkPos = Shader.PropertyToID("ChunkPos");
        
        private const int BufferStride = 16;
        private const int MeshVertexStride = 32;

        public static bool Native { get; set; } = true;
        public Mesh Mesh { get; private set; }

        private GraphicsBuffer buffer;
        private int bufferSize;

        private IndexPos pos;
        private int lod;
        private int reference;

        public MeshInterface(IndexPos pos, int lod) {
            this.pos = pos;
            this.lod = lod;
        }

        public MeshInterface(IndexPos pos, int lod, byte[] data) {
            this.pos = pos;
            this.lod = lod;
            // Recreate Buffers {}
        }

        public void Compose(ComputeShader shader, int kernelMesh, int kernelBuffer, int vertexCount, int index) {
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
            if (reference <= 0) {
                GameManager.Instance.RunSync(Dispose);
            }
        }
        
        public void Dispose() {
            if (buffer != null) {
                buffer.Dispose();
                buffer = null;
            }

            if (Mesh != null) {
                UnityEngine.Object.Destroy(Mesh);
                Mesh = null;
            }

            propertyBlock = null;
        }
        
        private void ComposeBuffer(ComputeShader shader, int kernel, int vertexCount, int index) {
            buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, vertexCount, BufferStride);
            bufferSize = vertexCount;
            
            propertyBlock = new MaterialPropertyBlock();
            propertyBlock.SetBuffer(MeshVertexBuffer, buffer);
            propertyBlock.SetVector(ChunkPos, new Vector4(pos.x, pos.y, pos.z, 1 << lod));

            shader.SetBuffer(kernel, BufferVertexBuffer, buffer);

            shader.SetInts(VertexCount, vertexCount, 1, 0, index);
            shader.Dispatch(kernel, Mathf.CeilToInt(vertexCount / 64f), 1, 1);
        }

        private void ComposeMesh(ComputeShader shader, int kernel, int vertexCount, int index) {
            bufferSize = vertexCount;
            
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

            if (Native) {
                RecreateBuffer(data);
            } else {
                RecreateMesh(data);
            }
        }
        
        private void RecreateBuffer(byte[] data) {
            if (data.Length % BufferStride != 0)
                throw new ArgumentException($"Invalid buffer data size: {data.Length}");

            int vertexCount = data.Length / BufferStride;

            buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, vertexCount, BufferStride);

            bufferSize = vertexCount;

            buffer.SetData(data);

            propertyBlock = new MaterialPropertyBlock();
            propertyBlock.SetBuffer(MeshVertexBuffer, buffer);
            propertyBlock.SetVector(ChunkPos, new Vector4(pos.x, pos.y, pos.z, 1 << lod));
        }

        private void RecreateMesh(byte[] data) {
            if (data.Length % MeshVertexStride != 0)
                throw new ArgumentException($"Invalid mesh data size: {data.Length}. ");

            int vertexCount = data.Length / MeshVertexStride;

            bufferSize = vertexCount;

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

        private MaterialPropertyBlock propertyBlock;

        public void Render(RenderParams renderParams) {
            if (Native) {
                RenderBuffer(renderParams);
            } else {
                RenderMesh(renderParams);
            }
        }
        
        private void RenderBuffer(RenderParams renderParams) {
            if (buffer == null) return;

            renderParams.matProps = propertyBlock;
            renderParams.worldBounds = new Bounds(
                new Vector3(16, 16, 16) + (Vector3)pos,
                new Vector3(32, 32, 32) * (1 << lod)
            );

            Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, bufferSize);
        }
        
        private void RenderMesh(RenderParams renderParams) {
            if (Mesh == null) return;
            
            var matrix = Matrix4x4.TRS((Vector3)pos, Quaternion.identity, Vector3.one * (1 << lod));
            Graphics.RenderMesh(renderParams, Mesh, 0, matrix);
        }

        public void RequestDataAsync(Action<byte[]> action) {
            if (Native) {
                RequestBufferDataAsync(action);
            } else {
                RequestMeshDataAsync(action);
            }
        }

        private void RequestBufferDataAsync(Action<byte[]> action) {
            AsyncGPUReadback.Request(buffer, request => {
                if (request.hasError) {
                    action.Invoke(null);
                    return;
                }

                var vertices = request.GetData<Vector4>();
                    
                byte[] bytes = vertices.Reinterpret<byte>(Marshal.SizeOf<Vector4>()).ToArray();
                    
                action.Invoke(bytes);
            });
        }

        private void RequestMeshDataAsync(Action<byte[]> action) {
            var gBuffer = Mesh.GetVertexBuffer(0);

            AsyncGPUReadback.Request(gBuffer, request => {
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
            if (Native) {
                return RequestBufferData();
            } else {
                return RequestMeshData();
            }
        }
        
        private byte[] RequestMeshData() {
            if (Mesh == null) return null;

            byte[] data = new byte[bufferSize * 32];
            
            var gBuffer = Mesh.GetVertexBuffer(0);
            try {
                gBuffer.GetData(data);
            } finally {
                gBuffer.Dispose();
            }

            return data;
        }
        
        private byte[] RequestBufferData() {
            if (buffer == null) return null;

            byte[] data = new byte[bufferSize * 4];
            buffer.GetData(data);

            return data;
        }
    }
}