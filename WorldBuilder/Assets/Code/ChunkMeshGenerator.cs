using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Code {
	public delegate void ChunkRemeshListener(Mesh mesh);

	public class ChunkMeshGenerator {
		private static uint[] emptyDensity = new uint[Chunk.SIZE_3 / 8];
		private static uint[] emptyMaterial = new uint[Chunk.SIZE_3 / 4];
		
		// Source
		private GraphicsBuffer voxelBuffer;
		private GraphicsBuffer chunkBuffer;
		private GraphicsBuffer materialBuffer;
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

			/*
			 * Size :
			 *  LOD 0 - 3 * 3 * 3 (1 + 2) = 27
			 *  LOD 1 - 4 * 4 * 4 (2 + 2) = 64
			 *  LOD 2 - 6 * 6 * 6 (4 + 2) = 216
			 * Each Voxel uses 4 bits (32bit / 8 = 4bit) - I used uint because it is GPU friendly
			 */
			voxelBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Chunk.SIZE_3 / 8 * (6 * 6 * 6), sizeof(uint));
			materialBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Chunk.SIZE_3 / 4 * (6 * 6 * 6), sizeof(uint));
			chunkBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 6 * 6 * 6, sizeof(uint));

			// 3 * 18 = Max Vertex per Voxel
			vertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Chunk.SIZE_4 * (3 * 18), sizeof(float) * (3 + 3 + 4 + 4 + 4 + 4)); 
			extraCounter = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(int));
			vertexCounter = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(int));
			voxelsCounter = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Chunk.SIZE_4, sizeof(int) * 2);

			triangleTable = new GraphicsBuffer(GraphicsBuffer.Target.Structured, TriangleTable.Table.Length, sizeof(int));
			triangleTable.SetData(TriangleTable.Table);

			// Input
			shader.SetBuffer(buildVertex, "TriangleTable", triangleTable);
			shader.SetBuffer(buildVertex, "VoxelBuffer", voxelBuffer);
			shader.SetBuffer(buildVertex, "MaterialBuffer", materialBuffer);
			shader.SetBuffer(buildVertex, "ChunkBuffer", chunkBuffer);
			
			// Output
			shader.SetBuffer(buildVertex, "VertexBuffer", vertexBuffer);		// List<GeneratedVertex>
			shader.SetBuffer(buildVertex, "ExtraCounter", extraCounter);		// Vertex indices from padding
			shader.SetBuffer(buildVertex, "VertexCounter", vertexCounter);		// Vertex indices from content
			shader.SetBuffer(buildVertex, "VoxelsCounter", voxelsCounter);		// List<Start Index, Triangle Count>
			
			shader.SetBuffer(buildMesh, "MeshInput", vertexBuffer);				// Input from buildVertex[VertexCounter]
			shader.SetBuffer(buildMesh, "MeshCounter", voxelsCounter);			// Input from buildVertex[VoxelsCounter]
			
			// The VoxelsCounter is a list of all voxels, indexed by position. It is useful to calculate normals

		}

		public void Release() {
			triangleTable.Release();
			voxelBuffer.Release();
			vertexBuffer.Release();
			materialBuffer.Release();
			vertexCounter.Release();
			voxelsCounter.Release();
			chunkBuffer.Release();
			extraCounter.Release();
		}

		public void Remesh(Chunk chunk, Chunk soil, ChunkRemeshListener onChunkRemesh) {
			int sizeD = Chunk.SIZE_3 / 8;
			voxelBuffer.SetData(emptyDensity);
			voxelBuffer.SetData(chunk.density, 0, sizeD * 1, chunk.density.Length);
			voxelBuffer.SetData(soil.density, 0, sizeD * 2, soil.density.Length);
			
			int sizeM = Chunk.SIZE_3 / 4;
			materialBuffer.SetData(emptyMaterial);
			materialBuffer.SetData(chunk.material, 0, sizeM * 1, chunk.material.Length);
			materialBuffer.SetData(soil.material, 0, sizeM * 2, soil.material.Length);
			
			uint[] chunkIndex = new uint[6 * 6 * 6];
			
			for (int px = 0; px < 3; px++) {
				for (int pz = 0; pz < 3; pz++) {
					chunkIndex[px + 1 * 36 + pz * 6] = 0;
				}
			}
			chunkIndex[1 + 36 + 6] = (uint)sizeD;
			
			chunkBuffer.SetData(chunkIndex);
			vertexCounter.SetData(new uint[] { 0 });
			extraCounter.SetData(new uint[] { Chunk.SIZE_4 * (3 * 18) });
			
			shader.SetInts("chunk_pos", 32, 32, 32, 0);
			shader.Dispatch(buildVertex, 17, 17, 17);

			AsyncGPUReadback.Request(vertexCounter, (request) => {
				var data = request.GetData<uint>();
				int indexCount = (int)data[0];
				if (indexCount == 0) {
					onChunkRemesh?.Invoke(null);
				} else {
					ComposeMesh(indexCount, onChunkRemesh);
				}
			});
		}

		private void ComposeMesh(int vertexCount, ChunkRemeshListener onChunkRemesh) {
			Mesh mesh = new Mesh();
			mesh.indexFormat = IndexFormat.UInt32;
			mesh.indexBufferTarget = GraphicsBuffer.Target.Structured;
			mesh.SetVertexBufferParams(vertexCount,
				new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float16, 4),
				new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float16, 4),
				new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 4),
				new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float16, 4),
				new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float16, 4)
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
				onChunkRemesh?.Invoke(mesh);
			});
		}
	}
    
	[StructLayout(LayoutKind.Sequential)]
	public struct GeneratedVertex {
		public Vector3Int position;
		public Vector3 normal;
		public Vector4 uv0;
		public Vector4 uv1;
		public Vector4 uv2;
		public Vector4 uv3;

		public override string ToString() {
			return $"Pos=({position.x}, {position.y}, {position.z}) " +
			       $"Nor=({normal.x}, {normal.y}, {normal.z}) " +
			       $"UV0=({uv0.x}, {uv0.y}, {uv0.z}, {uv0.w})" +
			       $"UV1=({uv1.x}, {uv1.y}, {uv1.z}, {uv1.w})";
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
	    public uint uv10;
	    public uint uv11;
	    public uint uv20;
	    public uint uv21;
		
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

		    ushort u4 = (ushort)(uv10 & 0xFFFF);
		    ushort u5 = (ushort)(uv10 >> 16);

		    ushort u6 = (ushort)(uv11 & 0xFFFF);
		    ushort u7 = (ushort)(uv11 >> 16);

		    return position0 + ", " + position1 + " = " +
		           $"Pos=({HalfToFloat(px)}, {HalfToFloat(py)}, {HalfToFloat(pz)}, {HalfToFloat(pw)}) " +
		           $"Nor=({HalfToFloat(nx)}, {HalfToFloat(ny)}, {HalfToFloat(nz)}, {HalfToFloat(nw)}) " +
		           $"UV0=({HalfToFloat(u0)}, {HalfToFloat(u1)}, {HalfToFloat(u2)}, {HalfToFloat(u3)}) " +
		           $"UV1=({HalfToFloat(u4)}, {HalfToFloat(u5)}, {HalfToFloat(u6)}, {HalfToFloat(u7)}) ";
	    }
    }
    
    public static class MeshDebugData
    {
	    public static Vector3[] Vertices;
	    public static Vector3[] Normals;
    }
}