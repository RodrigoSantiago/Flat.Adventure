using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Code {
	public delegate void ChunkRemeshListener(Mesh mesh);
	public delegate void ChunkBufferListener(GraphicsBuffer VertexBuffer, int VertexCount);

	public class ChunkMeshGenerator {
		private static uint[] emptyChunk = new uint[Chunk.SIZE_3 / 4];
		
		// Source
		private GraphicsBuffer voxelBuffer;
		private GraphicsBuffer chunkBuffer;
		private GraphicsBuffer triangleTable;

		// Destination
		private GraphicsBuffer vertexBuffer;
		private GraphicsBuffer extraCounter;
		private GraphicsBuffer vertexCounter;
		private GraphicsBuffer voxelsCounter;

		private ComputeShader shader;

		private int buildVertex;
		private int buildMesh;

		public ChunkMeshGenerator(ComputeShader shader) {
			this.shader = shader;
		}
		
		public void Init() {
			buildVertex = shader.FindKernel("BuildVertex");
			buildMesh = shader.FindKernel("BuildMesh");

			voxelBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Chunk.SIZE_3 * 216 / 2, sizeof(uint));
			chunkBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 216, sizeof(uint));

			vertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Chunk.SIZE_4 * (3 * 18), sizeof(float) * (3 + 3 + 4));
			extraCounter = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(int));
			vertexCounter = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(int));
			voxelsCounter = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Chunk.SIZE_4, sizeof(int) * 2);

			triangleTable = new GraphicsBuffer(GraphicsBuffer.Target.Structured, TriangleTable.Table.Length, sizeof(int));
			triangleTable.SetData(TriangleTable.Table);

			// Input
			shader.SetBuffer(buildVertex, "TriangleTable", triangleTable);
			shader.SetBuffer(buildVertex, "VoxelBuffer", voxelBuffer);
			shader.SetBuffer(buildVertex, "ChunkBuffer", chunkBuffer);
			
			// Output
			shader.SetBuffer(buildVertex, "VertexBuffer", vertexBuffer);
			shader.SetBuffer(buildVertex, "ExtraCounter", extraCounter);
			shader.SetBuffer(buildVertex, "VertexCounter", vertexCounter);
			shader.SetBuffer(buildVertex, "VoxelsCounter", voxelsCounter);
			
			shader.SetBuffer(buildMesh, "MeshInput", vertexBuffer);
			shader.SetBuffer(buildMesh, "MeshCounter", voxelsCounter);

		}

		public void Release() {
			triangleTable.Release();
			voxelBuffer.Release();
			vertexBuffer.Release();
			vertexCounter.Release();
			voxelsCounter.Release();
			chunkBuffer.Release();
			extraCounter.Release();
		}

		public void Remesh(Chunk chunk, Chunk soil, ChunkRemeshListener OnChunkRemesh) {
			int size = Chunk.SIZE_3 / 4;
			uint x = Chunk.SIZE_3 / 4;
			voxelBuffer.SetData(emptyChunk);
			voxelBuffer.SetData(chunk.density, 0, size * 1, chunk.density.Length);
			voxelBuffer.SetData(soil.density, 0, size * 2, soil.density.Length);
			
			uint[] chunkIndex = new uint[6 * 6 * 6];
			
			for (int px = 0; px < 3; px++) {
				for (int pz = 0; pz < 3; pz++) {
					chunkIndex[px + 1 * 36 + pz * 6] = x * 2;
				}
			}
			chunkIndex[1 + 36 + 6] = x;
			
			chunkBuffer.SetData(chunkIndex);
			vertexCounter.SetData(new uint[] { 0 });
			extraCounter.SetData(new uint[] { Chunk.SIZE_4 * (3 * 18) });
			
			shader.SetInts("chunk_pos", 32, 32, 32, 0);
			shader.Dispatch(buildVertex, 17, 17, 17);

			AsyncGPUReadback.Request(vertexCounter, (request) => {
				var data = request.GetData<uint>();
				int indexCount = (int)data[0];
				if (indexCount == 0) {
					OnChunkRemesh?.Invoke(null);
				} else {
					ComposeMesh(indexCount, OnChunkRemesh);
				}
			});
		}

		private void ComposeMesh(int vertexCount, ChunkRemeshListener OnChunkRemesh) {
			Mesh mesh = new Mesh();
			mesh.indexFormat = IndexFormat.UInt32;
			mesh.indexBufferTarget = GraphicsBuffer.Target.Structured;
			mesh.SetVertexBufferParams(vertexCount,
				new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float16, 4),
				new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float16, 4),
				new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 4)
			);
			mesh.SetIndexBufferParams(vertexCount, IndexFormat.UInt32);
			mesh.subMeshCount = 1;
			mesh.SetSubMesh(0, new SubMeshDescriptor(0, vertexCount), MeshUpdateFlags.DontRecalculateBounds);
			mesh.vertexBufferTarget = GraphicsBuffer.Target.Structured;

			mesh.bounds = new Bounds(new Vector3(16, 16, 16), new Vector3(32, 32, 32));

			var meshVertex = mesh.GetVertexBuffer(0);
			var meshIndex = mesh.GetIndexBuffer();

			shader.SetBuffer(buildMesh, "MeshVertexBuffer", meshVertex);
			shader.SetBuffer(buildMesh, "MeshIndexBuffer", meshIndex);
			
			shader.SetInts("vertex_count", vertexCount, 0, 0, 0);
			shader.SetInts("chunk_pos", 0, 0, 0, 0);
			shader.Dispatch(buildMesh, Mathf.CeilToInt(vertexCount / 64f), 1, 1);

			AsyncGPUReadback.Request(meshVertex, (request) => {
				meshVertex.Dispose();
				meshIndex.Dispose();
				OnChunkRemesh?.Invoke(mesh);
			});
		}
	
		private void toDebug(AsyncGPUReadbackRequest request) {
			var gpuVertices = request.GetData<GeneratedVertexLow>();

			MeshDebugData.Vertices = new Vector3[gpuVertices.Length];
			MeshDebugData.Normals = new Vector3[gpuVertices.Length];

			for (int i = 0; i < gpuVertices.Length; i++) {
				GeneratedVertexLow v = gpuVertices[i];

				ushort px = (ushort)(v.position0 & 0xFFFF);
				ushort py = (ushort)(v.position0 >> 16);
				ushort pz = (ushort)(v.position1 & 0xFFFF);

				ushort nx = (ushort)(v.normal0 & 0xFFFF);
				ushort ny = (ushort)(v.normal0 >> 16);
				ushort nz = (ushort)(v.normal1 & 0xFFFF);

				MeshDebugData.Vertices[i] = new Vector3(
					Mathf.HalfToFloat(px),
					Mathf.HalfToFloat(py),
					Mathf.HalfToFloat(pz)
				);

				MeshDebugData.Normals[i] = new Vector3(
					Mathf.HalfToFloat(nx),
					Mathf.HalfToFloat(ny),
					Mathf.HalfToFloat(nz)
				).normalized;
			}
		}
	}

	[StructLayout(LayoutKind.Sequential)]
    public struct GeneratedVertexLow {
	    public uint position0;
	    public uint position1;

	    public uint normal0;
	    public uint normal1;

	    public uint uv00;
	    public uint uv01;
		
	    private static float HalfToFloat(ushort value) {
		    return Mathf.HalfToFloat(value);
	    }

	    public override string ToString() {
		    ushort px = (ushort)(position0 & 0xFFFF);
		    ushort py = (ushort)(position0 >> 16);

		    ushort pz = (ushort)(position1 & 0xFFFF);
		    ushort pw = (ushort)(position1 >> 16);

		    ushort nx = (ushort)(normal0 & 0xFFFF);
		    ushort ny = (ushort)(normal0 >> 16);

		    ushort nz = (ushort)(normal1 & 0xFFFF);
		    ushort nw = (ushort)(normal1 >> 16);

		    ushort u0 = (ushort)(uv00 & 0xFFFF);
		    ushort u1 = (ushort)(uv00 >> 16);

		    ushort u2 = (ushort)(uv01 & 0xFFFF);
		    ushort u3 = (ushort)(uv01 >> 16);

		    return position0 + ", " + position1 + " = " +
		           $"Pos=({HalfToFloat(px)}, {HalfToFloat(py)}, {HalfToFloat(pz)}, {HalfToFloat(pw)}) " +
		           $"Nor=({HalfToFloat(nx)}, {HalfToFloat(ny)}, {HalfToFloat(nz)}, {HalfToFloat(nw)}) " +
		           $"UV=({HalfToFloat(u0)}, {HalfToFloat(u1)}, {HalfToFloat(u2)}, {HalfToFloat(u3)})";
	    }
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct GeneratedVertex {
	    public Vector3Int position;
	    public Vector3 normal;
	    public Vector4 uv0;
    }
    
    public static class MeshDebugData
    {
	    public static Vector3[] Vertices;
	    public static Vector3[] Normals;
    }
}