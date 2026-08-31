using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Code;
using Game.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.GraphicGenerator {
	public class ChunkMeshGenerator : MonoBehaviour {
		public static ChunkMeshGenerator Instance { get; private set; }
		
		private static readonly ChunkSoil Empty = new();
		
		// Source
		private ComputeBuffer densityBuffer;
		private ComputeBuffer materialBuffer;
		private GraphicsBuffer chunkBuffer;
		private GraphicsBuffer triangleTable;

		// Destination
		private GraphicsBuffer vertexBuffer;
		private GraphicsBuffer extraCounter;
		private GraphicsBuffer vertexCounter;
		private GraphicsBuffer voxelsCounter;

		[SerializeField] private ComputeShader shader;
		[SerializeField] private Material groundMaterial;

		public Material GroundMaterial => groundMaterial;


		private struct ChunkDataEntry {
			public IndexPos pos;
			public int lod;
			public int version;
			public bool reserved;
		}
		
		private readonly ChunkDataEntry[] entries = new ChunkDataEntry[27];
		private readonly uint[] chunkIndex = new uint[27];

		private int buildVertex;
		private int buildMesh;
		
		private static readonly int MeshVertexBuffer = Shader.PropertyToID("MeshVertexBuffer");
		private static readonly int MeshIndexBuffer = Shader.PropertyToID("MeshIndexBuffer");
		private static readonly int Table = Shader.PropertyToID("TriangleTable");
		private static readonly int DensityBuffer = Shader.PropertyToID("DensityBuffer");
		private static readonly int MaterialBuffer = Shader.PropertyToID("MaterialBuffer");
		private static readonly int ChunkBuffer = Shader.PropertyToID("ChunkBuffer");
		private static readonly int VertexBuffer = Shader.PropertyToID("VertexBuffer");
		private static readonly int ExtraCounter = Shader.PropertyToID("ExtraCounter");
		private static readonly int VertexCounter = Shader.PropertyToID("VertexCounter");
		private static readonly int VoxelsCounter = Shader.PropertyToID("VoxelsCounter");
		private static readonly int MeshInput = Shader.PropertyToID("MeshInput");
		private static readonly int MeshCounter = Shader.PropertyToID("MeshCounter");
		
		private static readonly int ChunkPos = Shader.PropertyToID("chunk_pos");
		private static readonly int VertexCount = Shader.PropertyToID("vertex_count");
		
		private static readonly int MaxDen = ChunkSoil.Mode.Packed4.DenArraySize; // 16388
		private static readonly int MaxMat = ChunkSoil.Mode.Packed6.MatArraySize; // 24580

		private Queue<Action> queue = new();

		private void Awake() {
			Instance = this;
			Init();
		}

		public void Init() {
			buildVertex = shader.FindKernel("BuildVertex");
			buildMesh = shader.FindKernel("BuildMesh");

			/*
			 * Size :
			 *  LOD 0 - 3 * 3 * 3 (1 + 2) = 27
			 *  LOD 1 - 4 * 4 * 4 (2 + 2) = 64
			 *  LOD 2 - 6 * 6 * 6 (4 + 2) = 216
			 */
			
			var str = GraphicsBuffer.Target.Structured;

			int maxChunkCount = 27; // Only LOD 0 by now
			
			densityBuffer = new ComputeBuffer(MaxDen * maxChunkCount / 4, sizeof(uint), ComputeBufferType.Raw);
			materialBuffer = new ComputeBuffer(MaxMat * maxChunkCount / 4, sizeof(uint), ComputeBufferType.Raw);
			chunkBuffer = new GraphicsBuffer(str, maxChunkCount, sizeof(uint));

			// 3 * 5 = Max Vertex per Voxel
			vertexBuffer = new GraphicsBuffer(str, ChunkSoil.Size4D * 15, sizeof(float) * (3 + 3 + 4 + 4));
			extraCounter = new GraphicsBuffer(str, 1, sizeof(int));
			vertexCounter = new GraphicsBuffer(str, 1, sizeof(int));
			voxelsCounter = new GraphicsBuffer(str, ChunkSoil.Size4D, sizeof(int) * 2);

			triangleTable = new GraphicsBuffer(str, TriangleTable.Table.Length, sizeof(int));
			triangleTable.SetData(TriangleTable.Table);

			// Step: Build Vertex
			// Input
			shader.SetBuffer(buildVertex, Table, triangleTable);
			shader.SetBuffer(buildVertex, DensityBuffer, densityBuffer);
			shader.SetBuffer(buildVertex, MaterialBuffer, materialBuffer);
			shader.SetBuffer(buildVertex, ChunkBuffer, chunkBuffer);

			// Output
			shader.SetBuffer(buildVertex, VertexBuffer, vertexBuffer);		// List<GeneratedVertex>
			shader.SetBuffer(buildVertex, VertexCounter, vertexCounter);	// Vertex counter for content
			shader.SetBuffer(buildVertex, ExtraCounter, extraCounter);		// Vertex counter for padding
			shader.SetBuffer(buildVertex, VoxelsCounter, voxelsCounter);	// List<Start Index, Triangle Count>

			// Step: Build Mesh
			// Input
			shader.SetBuffer(buildMesh, MeshInput, vertexBuffer);			// Input from "Build Vertex"(VertexCounter)
			shader.SetBuffer(buildMesh, MeshCounter, voxelsCounter);		// Input from "Build Vertex"(VoxelsCounter)
		}

		public void Release() {
			triangleTable.Release();
			densityBuffer.Release();
			vertexBuffer.Release();
			materialBuffer.Release();
			vertexCounter.Release();
			voxelsCounter.Release();
			chunkBuffer.Release();
			extraCounter.Release();
		}

		public void SimpleMesh(ChunkSoil chunk, ChunkSoil soil, Action<Mesh> onChunkRemesh) {
			densityBuffer.SetData(Empty.density, 0, 0, Empty.density.Length);
			densityBuffer.SetData(chunk.density, 0, MaxDen * 1, chunk.density.Length);
			densityBuffer.SetData(soil.density, 0, MaxDen * 2, soil.density.Length);

			materialBuffer.SetData(Empty.material, 0, 0, Empty.material.Length);
			materialBuffer.SetData(chunk.material, 0, MaxMat * 1, chunk.material.Length);
			materialBuffer.SetData(soil.material, 0, MaxMat * 2, soil.material.Length);

			uint[] chunkIndex = new uint[27];

			for (int px = 0; px < 3; px++) {
				for (int pz = 0; pz < 3; pz++) {
					chunkIndex[px + 1 * 9 + pz * 3] = 0;
				}
			}

			chunkIndex[1 + 9 + 3] = 1;

			chunkBuffer.SetData(chunkIndex);
			vertexCounter.SetData(new uint[] { 0 });
			extraCounter.SetData(new uint[] { ChunkSoil.Size3D * 15 });

			shader.SetInts(ChunkPos, 32, 32, 32, 0);
			shader.Dispatch(buildVertex, 9, 9, 9);

			AsyncGPUReadback.Request(vertexCounter, (request) => {
				var data = request.GetData<uint>();
				int indexCount = (int)data[0];
				if (indexCount == 0) {
					onChunkRemesh?.Invoke(null);
				} else {
					ComposeMesh(indexCount, onChunkRemesh);
				}
			});
			
			for (int i = 0; i < entries.Length; i++) {
				entries[i].reserved = false;
				entries[i].lod = -2;
			}
		}

		private int UploadChunkEntry(Chunk chunk) {
			if (chunk == null || chunk.Soil.IsEmpty()) {
				for (int i = 0; i < entries.Length; i++) {
					var entry = entries[i];
					if (entry.lod == -1) {
						entry.reserved = true;
						entries[i] = entry;
						return i;
					}
				}
				for (int i = 0; i < entries.Length; i++) {
					var entry = entries[i];
					if (!entry.reserved) {
						entry.lod = -1;
						entry.reserved = true;
						entries[i] = entry;
						densityBuffer.SetData(Empty.density, 0, MaxDen * i, Empty.density.Length);
						materialBuffer.SetData(Empty.material, 0, MaxMat * i, Empty.material.Length);
						return i;
					}
				}

				return -1;
			}
			
			for (int i = 0; i < entries.Length; i++) {
				var entry = entries[i];
				if (entry.lod == chunk.Lod && entry.pos == chunk.Pos && entry.version == chunk.CurrentVersion) {
					entry.reserved = true;
					entries[i] = entry;
					return i;
				}
			}
			for (int i = 0; i < entries.Length; i++) {
				var entry = entries[i];
				if (!entry.reserved) {
					entry.lod = chunk.Lod;
					entry.pos = chunk.Pos;
					entry.version = chunk.CurrentVersion;
					entry.reserved = true;
					entries[i] = entry;
					densityBuffer.SetData(chunk.Soil.density, 0, MaxDen * i, chunk.Soil.density.Length);
					materialBuffer.SetData(chunk.Soil.material, 0, MaxMat * i, chunk.Soil.material.Length);
					return i;
				}
			}

			return -1;
		}

		private bool working;

		public void Remesh(Chunk[] chunks, Action<Mesh> onChunkRemesh) {
			if (!working) {
				RemeshNow(chunks, onChunkRemesh);
			} else {
				queue.Enqueue(() => RemeshNow(chunks, onChunkRemesh));
			}
		}

		private void RemeshQueue() {
			if (queue.TryDequeue(out var act)) {
				act.Invoke();
			} else {
				working = false;
			}
		}

		private void RemeshNow(Chunk[] chunks, Action<Mesh> onChunkRemesh) {
			working = true;
			for (int i = 0; i < entries.Length; i++) {
				entries[i].reserved = false;
			}
			
			for (var i = 0; i < chunks.Length; i++) {
				var chunk = chunks[i];
				chunkIndex[i] = (uint)UploadChunkEntry(chunk);
			}
			
			chunkBuffer.SetData(chunkIndex);
			vertexCounter.SetData(new uint[] { 0 });
			extraCounter.SetData(new uint[] { ChunkSoil.Size3D * 15 });

			shader.SetInts(ChunkPos, 32, 32, 32, 0);
			shader.Dispatch(buildVertex, 9, 9, 9);

			AsyncGPUReadback.Request(vertexCounter, (request) => {
				var data = request.GetData<uint>();
				int indexCount = (int)data[0];
				if (indexCount == 0) {
					try {
						onChunkRemesh?.Invoke(null);
					} finally {
						RemeshQueue();
					}
				} else {
					ComposeMesh(indexCount, onChunkRemesh);
				}
			});
		}

		private void ComposeMesh(int vertexCount, Action<Mesh> onChunkRemesh) {
			var mesh = new Mesh();
			mesh.indexFormat = IndexFormat.UInt32;
			mesh.indexBufferTarget |= GraphicsBuffer.Target.Structured;
			mesh.SetVertexBufferParams(vertexCount,
				new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float16, 4),
				new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float16, 4),
				new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 4),
				new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float16, 4)
			);
			mesh.SetIndexBufferParams(vertexCount, IndexFormat.UInt32);
			mesh.subMeshCount = 1;
			mesh.SetSubMesh(0, new SubMeshDescriptor(0, vertexCount), MeshUpdateFlags.DontRecalculateBounds);
			mesh.vertexBufferTarget = GraphicsBuffer.Target.Structured;

			mesh.bounds = new Bounds(new Vector3(16, 16, 16), new Vector3(32, 32, 32));

			var meshVertex = mesh.GetVertexBuffer(0);
			var meshIndex = mesh.GetIndexBuffer();

			try {
				shader.SetBuffer(buildMesh, MeshVertexBuffer, meshVertex);
				shader.SetBuffer(buildMesh, MeshIndexBuffer, meshIndex);

				shader.SetInts(VertexCount, vertexCount, 0, 0, 0);
				shader.SetInts(ChunkPos, 0, 0, 0, 0);
				shader.Dispatch(buildMesh, Mathf.CeilToInt(vertexCount / 64f), 1, 1);
				
				onChunkRemesh?.Invoke(mesh);
				
			} finally {
				meshVertex.Dispose();
				meshIndex.Dispose();
				
				RemeshQueue();
			}
		}

		private void BuildCpuMesh(int vertexCount, GraphicsBuffer meshVertex) {
			AsyncGPUReadback.Request(meshVertex, request => {
				if (request.hasError) {
					meshVertex.Dispose();
					return;
				}

				var vertexUInts = request.GetData<uint>().ToArray();

				GeneratedVertexLow[] vertices =
					new GeneratedVertexLow[vertexCount];

				for (int i = 0; i < vertexCount; i++) {
					int offset = i * 8;

					vertices[i].position0 = vertexUInts[offset + 0];
					vertices[i].position1 = vertexUInts[offset + 1];

					vertices[i].normal0 = vertexUInts[offset + 2];
					vertices[i].normal1 = vertexUInts[offset + 3];

					vertices[i].uv00 = vertexUInts[offset + 4];
					vertices[i].uv01 = vertexUInts[offset + 5];

					vertices[i].uv10 = vertexUInts[offset + 6];
					vertices[i].uv11 = vertexUInts[offset + 7];
				}

				var positions = new Vector3[vertexCount];
				var normals = new Vector3[vertexCount];
				var uv0 = new Vector4[vertexCount];
				var uv1 = new Vector4[vertexCount];

				for (int i = 0; i < vertexCount; i++) {
					var vertex = vertices[i];

					ushort px = (ushort)(vertex.position0 & 0xFFFF);
					ushort py = (ushort)(vertex.position0 >> 16);
					ushort pz = (ushort)(vertex.position1 & 0xFFFF);

					ushort nx = (ushort)(vertex.normal0 & 0xFFFF);
					ushort ny = (ushort)(vertex.normal0 >> 16);
					ushort nz = (ushort)(vertex.normal1 & 0xFFFF);

					ushort u0 = (ushort)(vertex.uv00 & 0xFFFF);
					ushort u1 = (ushort)(vertex.uv00 >> 16);
					ushort u2 = (ushort)(vertex.uv01 & 0xFFFF);
					ushort u3 = (ushort)(vertex.uv01 >> 16);

					ushort u4 = (ushort)(vertex.uv10 & 0xFFFF);
					ushort u5 = (ushort)(vertex.uv10 >> 16);
					ushort u6 = (ushort)(vertex.uv11 & 0xFFFF);
					ushort u7 = (ushort)(vertex.uv11 >> 16);

					positions[i] = new Vector3(Mathf.HalfToFloat(px), Mathf.HalfToFloat(py), Mathf.HalfToFloat(pz));

					normals[i] = new Vector3(Mathf.HalfToFloat(nx), Mathf.HalfToFloat(ny), Mathf.HalfToFloat(nz));

					uv0[i] = new Vector4(
						Mathf.HalfToFloat(u0), Mathf.HalfToFloat(u1),
						Mathf.HalfToFloat(u2), Mathf.HalfToFloat(u3)
					);

					uv1[i] = new Vector4(
						Mathf.HalfToFloat(u4), Mathf.HalfToFloat(u5),
						Mathf.HalfToFloat(u6), Mathf.HalfToFloat(u7)
					);
				}

				int[] indices = new int[vertexCount];
				for (int i = 0; i < vertexCount; i++) {
					indices[i] = i;
				}

				var cpuMesh = new Mesh();
				cpuMesh.name = "ChunkMesh_CPU";
				cpuMesh.indexFormat = IndexFormat.UInt32;

				cpuMesh.vertices = positions;
				cpuMesh.normals = normals;
				cpuMesh.SetUVs(0, uv0);
				cpuMesh.SetUVs(1, uv1);
				cpuMesh.SetTriangles(indices, 0, false);
				cpuMesh.RecalculateBounds();

				var obj = new GameObject("ChunkMesh_CPU");
				var meshFilter = obj.AddComponent<MeshFilter>();
				meshFilter.sharedMesh = cpuMesh;
				obj.AddComponent<MeshRenderer>();
			});
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct GeneratedVertex {
		public Vector3Int position;
		public Vector3 normal;
		public Vector4 uv0;
		public Vector4 uv1;

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