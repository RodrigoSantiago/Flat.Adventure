using System;
using Game.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.GraphicGenerator {
    public class MeshInterface {
        private static readonly int BufferVertexBuffer = Shader.PropertyToID("BufferVertexBuffer");
        private static readonly int MeshVertexBuffer = Shader.PropertyToID("MeshVertexBuffer");
        private static readonly int MeshIndexBuffer = Shader.PropertyToID("MeshIndexBuffer");
        private static readonly int VertexCount = Shader.PropertyToID("vertex_count");
        private static readonly int ChunkPos = Shader.PropertyToID("ChunkPos");

        public static bool Native { get; set; } = true;
        public Mesh Mesh { get; private set; }

        private GraphicsBuffer buffer;
        private int bufferSize;

        private IndexPos pos;
        private int lod;

        public MeshInterface(IndexPos pos, int lod) {
            this.pos = pos;
            this.lod = lod;
        }

        public void Compose(ComputeShader shader, int kernelMesh, int kernelBuffer, int vertexCount, int index, Action<MeshInterface> action) {
            if (Native) {
                ComposeBuffer(shader, kernelBuffer, vertexCount, index, action);
            } else {
                ComposeMesh(shader, kernelMesh, vertexCount, index, action);
            }
        }

        public void Dispose() {
            if (buffer != null) {
                buffer.Dispose();
                buffer = null;
            }
        }

        private void ComposeBuffer(ComputeShader shader, int kernel, int vertexCount, int index, Action<MeshInterface> action) {
            buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, vertexCount, 16);
            bufferSize = vertexCount;
            
            propertyBlock = new MaterialPropertyBlock();
            propertyBlock.SetBuffer(MeshVertexBuffer, buffer);
            propertyBlock.SetVector(ChunkPos, new Vector4(pos.x, pos.y, pos.z, 1 << lod));

            shader.SetBuffer(kernel, BufferVertexBuffer, buffer);

            shader.SetInts(VertexCount, vertexCount, 1, 0, index);
            shader.Dispatch(kernel, Mathf.CeilToInt(vertexCount / 64f), 1, 1);
				
            action?.Invoke(this);
        }

        private void ComposeMesh(ComputeShader shader, int kernel, int vertexCount, int index, Action<MeshInterface> action) {
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
				
                action?.Invoke(this);
				
            } finally {
                meshVertex.Dispose();
                meshIndex.Dispose();
            }
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
    }
}