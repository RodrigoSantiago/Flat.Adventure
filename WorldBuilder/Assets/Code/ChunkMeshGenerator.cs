using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Code {
	public delegate void ChunkRemeshListener(Mesh mesh);
	public delegate void ChunkBufferListener(GraphicsBuffer VertexBuffer, int VertexCount);
	
    public class ChunkMeshGenerator {
        // Source
        private GraphicsBuffer voxelBuffer;
        private GraphicsBuffer triangleTable;

        // Destination
        private GraphicsBuffer vertexSolid;
        private GraphicsBuffer indexSolid;
        private GraphicsBuffer counterSolid;
        
        private ComputeShader shader;

        private int solid;
        private int bakeSolid;

        public ChunkMeshGenerator(ComputeShader shader) {
	        this.shader = shader;
        }

        public void Init() {
			solid = shader.FindKernel("Marche");
			bakeSolid = shader.FindKernel("BakeSolid");

			voxelBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Chunk.SIZE_3, sizeof(uint));

			vertexSolid = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Chunk.SIZE_3 * 3 * 18, sizeof(float) * (3 + 3 + 4));
			counterSolid = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(int));
			
			triangleTable = new GraphicsBuffer(GraphicsBuffer.Target.Structured, TriangleTable.Table.Length, sizeof(int));
			triangleTable.SetData(TriangleTable.Table);
			
			shader.SetBuffer(solid, "TriangleTable", triangleTable);
			shader.SetBuffer(solid, "VoxelBuffer", voxelBuffer);
			shader.SetBuffer(solid, "VertexSolid", vertexSolid);
			shader.SetBuffer(solid, "IndexSolid", indexSolid);
			shader.SetBuffer(solid, "CounterSolid", counterSolid);
			
			shader.SetBuffer(bakeSolid, "MeshInput", vertexSolid);
			
		}

		public void Release() {
			voxelBuffer.Release();
			vertexSolid.Release();
			counterSolid.Release();
			triangleTable.Release();
		}
		
		public void Remesh(Chunk chunk, ChunkRemeshListener OnChunkRemesh) {
			voxelBuffer.SetData(chunk.density);
			counterSolid.SetData(new uint[] { 0 });

			shader.Dispatch(solid, 8, 8, 8);

			AsyncGPUReadback.Request(counterSolid, (request) => {
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

			shader.SetBuffer(bakeSolid, "MeshVertexBuffer", meshVertex);
			shader.SetBuffer(bakeSolid, "MeshIndexBuffer", meshIndex);
			shader.Dispatch(bakeSolid, Mathf.CeilToInt(vertexCount / 64f), 1, 1);

			AsyncGPUReadback.Request(meshVertex, (request) =>
			{
				if (request.hasError)
				{
					meshVertex.Dispose();
					meshIndex.Dispose();
					return;
				}

				var gpuVertices = request.GetData<GeneratedVertexLow>();

				MeshDebugData.Vertices = new Vector3[gpuVertices.Length];
				MeshDebugData.Normals = new Vector3[gpuVertices.Length];

				for (int i = 0; i < gpuVertices.Length; i++)
				{
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

				meshVertex.Dispose();
				meshIndex.Dispose();

				OnChunkRemesh?.Invoke(mesh);
			});
			
			/*GeneratedVertexLow[] vertices = new GeneratedVertexLow[3];
			mesh.GetVertexBuffer(0).GetData(vertices);
			meshVertex.GetData(vertices);
			for (int i = 0; i < vertices.Length; i++) {
				DumpVertex(vertices[i], i);
			}
			Debug.Log(mesh.GetVertexBufferStride(0));
			Debug.Log(mesh.vertexCount);
			Debug.Log(mesh.GetIndexCount(0));
			meshVertex.Dispose();
			meshIndex.Dispose();*/
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
	    public Vector3 position;
	    public Vector3 normal;
	    public Vector4 uv0;
    }
    public static class MeshDebugData
    {
	    public static Vector3[] Vertices;
	    public static Vector3[] Normals;
    }
}