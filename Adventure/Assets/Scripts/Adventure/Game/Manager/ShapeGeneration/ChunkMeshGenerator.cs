using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adventure.Logic.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace Adventure.Game.Manager.ShapeGeneration {
	public class ChunkMeshGenerator {

		[StructLayout(LayoutKind.Sequential)]
		public struct GeneratedVertex {
			public Vector3 position;
			public Vector3 normal;
			public Vector3 uv0;
			public Vector3 uv1;

			public override string ToString() {
				return position + " " + normal + " " + uv0 + " " + uv1;
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct IVoxel {
			public float val;
			public float mat;

			public IVoxel(float val, float mat) {
				this.val = val;
				this.mat = mat;
			}
		}

		public delegate void ChunkRemeshListener(Chunk chunk, Mesh mesh);

		// Constants
		private const int CHUNK_GROUP_SIZE_01 = 16 * 16 * 16 * 8; // * 8 * 8;
		private const int CHUNK_GROUP_SIZE_02 = 16 * 16 * 16 * 8 * 8;
		private const int CHUNK_GROUP_SIZE_04 = 16 * 16 * 16 * 8;
		private const int CHUNK_GROUP_SIZE_08 = 16 * 16 * 16;
		private const int OFFSIZE = 20;

		// Per Mesh
		public Mesh meshA;
		public Mesh meshB;
		private IVoxel[] voxels;
		private Chunk currentChunk;
		private Chunk[] adjacentChunks;

		// Source
		private GraphicsBuffer voxelBuffer;

		// Destination
		private GraphicsBuffer vertexSolid;
		private GraphicsBuffer indexSolid;
		private GraphicsBuffer counterSolid;

		private GraphicsBuffer vertexLiquid;
		private GraphicsBuffer indexLiquid;
		private GraphicsBuffer counterLiquid;

		private ComputeShader shader;
		private ChunkRemeshListener OnChunkRemesh;

		private int solid, liquid, bakeSolid, bakeLiquid;
		private GraphicsBuffer triangleTable;

		private GeneratedVertex[] vertices;
		private GeneratedVertex[] verticesLiquid;
		private int indexCount = -1;
		private int indexCountLiquid = -1;
		public bool isRemeshing { get; private set; }

		public ChunkMeshGenerator(ComputeShader shader, ChunkRemeshListener chunkRemeshListener) {
			this.shader = shader;
			this.OnChunkRemesh = chunkRemeshListener;
			this.voxels = new IVoxel[OFFSIZE * OFFSIZE * OFFSIZE];
			this.adjacentChunks = new Chunk[27];
		}

		public void Init() {
			solid = shader.FindKernel("Marche");
			liquid = shader.FindKernel("Swim");
			bakeSolid = shader.FindKernel("BakeSolid");
			bakeLiquid = shader.FindKernel("BakeLiquid");

			triangleTable = new GraphicsBuffer(GraphicsBuffer.Target.Structured, TriangleConnectionTable.Length, sizeof(int));
			voxelBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, OFFSIZE * OFFSIZE * OFFSIZE, sizeof(float) * 2);

			vertexSolid = new GraphicsBuffer(GraphicsBuffer.Target.Structured, CHUNK_GROUP_SIZE_01 * 6, sizeof(float) * (3 + 3 + 3 + 3));
			indexSolid = new GraphicsBuffer(GraphicsBuffer.Target.Structured, CHUNK_GROUP_SIZE_01 * 6, sizeof(int));
			counterSolid = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(int));

			vertexLiquid = new GraphicsBuffer(GraphicsBuffer.Target.Structured, CHUNK_GROUP_SIZE_01 * 6, sizeof(float) * (3 + 3 + 3 + 3));
			indexLiquid = new GraphicsBuffer(GraphicsBuffer.Target.Structured, CHUNK_GROUP_SIZE_01 * 6, sizeof(int));
			counterLiquid = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(int));

			shader.SetBuffer(solid, "TriangleTable", triangleTable);
			shader.SetBuffer(solid, "VoxelBuffer", voxelBuffer);
			shader.SetBuffer(solid, "VertexSolid", vertexSolid);
			shader.SetBuffer(solid, "IndexSolid", indexSolid);
			shader.SetBuffer(solid, "CounterSolid", counterSolid);

			shader.SetBuffer(bakeSolid, "VertexSolid", vertexSolid);
			shader.SetBuffer(bakeSolid, "IndexSolid", indexSolid);

			shader.SetBuffer(liquid, "TriangleTable", triangleTable);
			shader.SetBuffer(liquid, "VoxelBuffer", voxelBuffer);
			shader.SetBuffer(liquid, "VertexLiquid", vertexLiquid);
			shader.SetBuffer(liquid, "IndexLiquid", indexLiquid);
			shader.SetBuffer(liquid, "CounterLiquid", counterLiquid);

			shader.SetBuffer(bakeLiquid, "VertexLiquid", vertexLiquid);
			shader.SetBuffer(bakeLiquid, "IndexLiquid", indexLiquid);

			triangleTable.SetData(TriangleConnectionTable);
		}

		public void Release() {
			triangleTable.Release();
			voxelBuffer.Release();
			vertexSolid.Release();
			indexSolid.Release();
			counterSolid.Release();
			vertexLiquid.Release();
			indexLiquid.Release();
			counterLiquid.Release();
			OnChunkRemesh = null;
		}

		public void RemeshChunk(Chunk chunk, Dictionary<Vector3Int, ChunkHolder> chunks) {
			this.currentChunk = chunk;
			this.isRemeshing = true;

			CreateDerivedChunk(voxels, chunk, chunks);

			/*for (int xx = 0; xx < 16; xx++)
			for (int yy = 0; yy < 16; yy++)
			for (int zz = 0; zz < 16; zz++) {
				IVoxel ivox = new IVoxel();
				Voxel vox = this.currentChunk[xx, yy, zz];
				ivox.mat = vox.Material;
				ivox.val = vox.Volume;
				voxels[(xx + 1) + (yy + 1) * 18 + (zz + 1) * 18 * 18] = ivox;
			}*/

			voxelBuffer.SetData(voxels);
			counterSolid.SetData(new uint[] { 0 });
			//counterLiquid.SetData(new uint[]{0});

			shader.Dispatch(solid, 2, 2, 2);
			//shader.Dispatch(liquid, x/8, y/8, z/8);

			AsyncGPUReadback.Request(counterSolid, OnSolidReady);
		}

		private bool first;
		private void CreateDerivedChunk(IVoxel[] voxels, Chunk currentChunk, Dictionary<Vector3Int, ChunkHolder> chunks) {
			int index = 0;
			for (int z = -1; z <= 1; z++) 
			for (int y = -1; y <= 1; y++) 
			for (int x = -1; x <= 1; x++) {
				Vector3Int position = currentChunk.local + new Vector3Int(x * 16, y * 16, z * 16);

				if (chunks.TryGetValue(position, out ChunkHolder adjacentChunk)) {
					adjacentChunks[index] = adjacentChunk.chunk;
				} else {
					adjacentChunks[index] = null;
				}

				index++;
			}

			int off = (OFFSIZE - 16) / 2;
			Vector3Int sPos = currentChunk.local - new Vector3Int(off, off, off);
			index = 0;
			for (int z = 0; z < OFFSIZE; z++) 
			for (int y = 0; y < OFFSIZE; y++) 
			for (int x = 0; x < OFFSIZE; x++) {
				Vector3Int inChunkPos = new Vector3Int((sPos.x + x) % 16, (sPos.y + y) % 16, (sPos.z + z) % 16);
				int ix = (x + 16 - off) / 16;
				int iy = (y + 16 - off) / 16;
				int iz = (z + 16 - off) / 16;
				
				Chunk chunk = adjacentChunks[ix + iy * 3 + iz * 9];
				var voxel = chunk == null ? new Voxel() : chunk[inChunkPos];
				int o = 0;
				if (x < o || x > OFFSIZE - 1 - o || y < o || y > OFFSIZE - 1 - o || z < o || z > OFFSIZE - 1 - o) {
					voxel = new Voxel();
				}
				voxels[index] = new IVoxel(voxel.Volume, voxel.Material);
				index++;
			}
			if (!first) {
				Debug.Log(sPos);
				Debug.Log(sPos + new Vector3Int(OFFSIZE,OFFSIZE,OFFSIZE));
				Debug.Log(">>"+voxels[0].val);
			}
			first = true;
		}

		private void OnSolidReady(AsyncGPUReadbackRequest request) {
			var data = request.GetData<uint>();
			indexCount = (int)data[0];
			if (indexCount > 0) {
				meshA = ComposeMesh(indexCount);
			}

			OnChunkRemesh?.Invoke(currentChunk, meshA);
			isRemeshing = false;
			meshA = null;
			indexCount = 0;
		}

		private Mesh ComposeMesh(int vertexCount) {
			Mesh mesh = new Mesh();
			mesh.indexFormat = IndexFormat.UInt32;
			mesh.indexBufferTarget = GraphicsBuffer.Target.Structured;
			mesh.SetVertexBufferParams(vertexCount,
				new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
				new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
				new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 3),
				new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 3)
			);
			mesh.SetIndexBufferParams(vertexCount, IndexFormat.UInt32);
			mesh.SetSubMesh(0, new SubMeshDescriptor(0, vertexCount), MeshUpdateFlags.DontRecalculateBounds);
			mesh.vertexBufferTarget = GraphicsBuffer.Target.Structured;

			mesh.bounds = new Bounds(new Vector3(8, 8, 8), new Vector3(16, 16, 16));

			var meshVertex = mesh.GetVertexBuffer(0);
			var meshIndex = mesh.GetIndexBuffer();

			shader.SetBuffer(bakeSolid, "MeshVertexBuffer", meshVertex);
			shader.SetBuffer(bakeSolid, "MeshIndexBuffer", meshIndex);
			shader.Dispatch(bakeSolid, Mathf.CeilToInt((vertexCount) / 64f), 1, 1);

			return mesh;
		}

		private Mesh ComposeMeshCpu(int vertexCount) {
			Mesh mesh = new Mesh();
			var arr = new GeneratedVertex[vertexCount];
			vertexSolid.GetData(arr);
			var ind = new int[vertexCount];
			indexSolid.GetData(ind);
			Vector3[] pos = new Vector3[vertexCount];
			Vector3[] nor = new Vector3[vertexCount];
			Vector3[] tex0 = new Vector3[vertexCount];
			Vector3[] tex1 = new Vector3[vertexCount];
			string s = "";
			for (int i = 0; i < vertexCount; i++) {
				pos[i] = arr[i].position;
				nor[i] = arr[i].normal;
				tex0[i] = arr[i].uv0;
				tex1[i] = arr[i].uv1;
				s += nor[i]+", "+ (i % 8 == 0? "\n":"");
			}
			Debug.Log(s);
			mesh.vertices = pos;
			mesh.normals = nor;
			mesh.triangles = ind;
			mesh.SetUVs(0, tex0);
			mesh.SetUVs(1, tex1);
			mesh.bounds = new Bounds(new Vector3(8, 8, 8), new Vector3(16, 16, 16));
			mesh.Optimize();
			return mesh;
		}

		public static void MeshToCpu(Mesh mesh) {
			var arr = new GeneratedVertex[mesh.vertexCount];
			Debug.Log(mesh.vertexCount);
			mesh.GetVertexBuffer(0).GetData(arr);
			Vector3[] pos = new Vector3[mesh.vertexCount];
			Vector3[] nor = new Vector3[mesh.vertexCount];
			Vector3[] tex0 = new Vector3[mesh.vertexCount];
			Vector3[] tex1 = new Vector3[mesh.vertexCount];
			for (int i = 0; i < mesh.vertexCount; i++) {
				pos[i] = arr[i].position;
				nor[i] = arr[i].normal;
				tex0[i] = arr[i].uv0;
				tex1[i] = arr[i].uv1;
			}

			mesh.vertices = pos;
			mesh.normals = nor;
			mesh.SetUVs(0, tex0);
			mesh.SetUVs(1, tex1);
		}

		private static readonly int[] TriangleConnectionTable = {
			-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			0, 1, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			1, 8, 3, 9, 8, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			0, 8, 3, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			9, 2, 10, 0, 2, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			2, 8, 3, 2, 10, 8, 10, 9, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			0, 11, 2, 8, 11, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			1, 9, 0, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			1, 11, 2, 1, 9, 11, 9, 8, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			3, 10, 1, 11, 10, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			0, 10, 1, 0, 8, 10, 8, 11, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			3, 9, 0, 3, 11, 9, 11, 10, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			9, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			4, 3, 0, 7, 3, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			0, 1, 9, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			4, 1, 9, 4, 7, 1, 7, 3, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			1, 2, 10, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			3, 4, 7, 3, 0, 4, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			9, 2, 10, 9, 0, 2, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			2, 10, 9, 2, 9, 7, 2, 7, 3, 7, 9, 4, -1, -1, -1, -1, -1, -1,
			8, 4, 7, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			11, 4, 7, 11, 2, 4, 2, 0, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			9, 0, 1, 8, 4, 7, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			4, 7, 11, 9, 4, 11, 9, 11, 2, 9, 2, 1, -1, -1, -1, -1, -1, -1,
			3, 10, 1, 3, 11, 10, 7, 8, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			1, 11, 10, 1, 4, 11, 1, 0, 4, 7, 11, 4, -1, -1, -1, -1, -1, -1,
			4, 7, 8, 9, 0, 11, 9, 11, 10, 11, 0, 3, -1, -1, -1, -1, -1, -1,
			4, 7, 11, 4, 11, 9, 9, 11, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			9, 5, 4, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			0, 5, 4, 1, 5, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			8, 5, 4, 8, 3, 5, 3, 1, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			1, 2, 10, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			3, 0, 8, 1, 2, 10, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			5, 2, 10, 5, 4, 2, 4, 0, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			2, 10, 5, 3, 2, 5, 3, 5, 4, 3, 4, 8, -1, -1, -1, -1, -1, -1,
			9, 5, 4, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			0, 11, 2, 0, 8, 11, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			0, 5, 4, 0, 1, 5, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			2, 1, 5, 2, 5, 8, 2, 8, 11, 4, 8, 5, -1, -1, -1, -1, -1, -1,
			10, 3, 11, 10, 1, 3, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			4, 9, 5, 0, 8, 1, 8, 10, 1, 8, 11, 10, -1, -1, -1, -1, -1, -1,
			5, 4, 0, 5, 0, 11, 5, 11, 10, 11, 0, 3, -1, -1, -1, -1, -1, -1,
			5, 4, 8, 5, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			9, 7, 8, 5, 7, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			9, 3, 0, 9, 5, 3, 5, 7, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			0, 7, 8, 0, 1, 7, 1, 5, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			1, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			9, 7, 8, 9, 5, 7, 10, 1, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			10, 1, 2, 9, 5, 0, 5, 3, 0, 5, 7, 3, -1, -1, -1, -1, -1, -1,
			8, 0, 2, 8, 2, 5, 8, 5, 7, 10, 5, 2, -1, -1, -1, -1, -1, -1,
			2, 10, 5, 2, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			7, 9, 5, 7, 8, 9, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			9, 5, 7, 9, 7, 2, 9, 2, 0, 2, 7, 11, -1, -1, -1, -1, -1, -1,
			2, 3, 11, 0, 1, 8, 1, 7, 8, 1, 5, 7, -1, -1, -1, -1, -1, -1,
			11, 2, 1, 11, 1, 7, 7, 1, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			9, 5, 8, 8, 5, 7, 10, 1, 3, 10, 3, 11, -1, -1, -1, -1, -1, -1,
			5, 7, 0, 5, 0, 9, 7, 11, 0, 1, 0, 10, 11, 10, 0, -1, -1, -1,
			11, 10, 0, 11, 0, 3, 10, 5, 0, 8, 0, 7, 5, 7, 0, -1, -1, -1,
			11, 10, 5, 7, 11, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			0, 8, 3, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			9, 0, 1, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			1, 8, 3, 1, 9, 8, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			1, 6, 5, 2, 6, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			1, 6, 5, 1, 2, 6, 3, 0, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			9, 6, 5, 9, 0, 6, 0, 2, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			5, 9, 8, 5, 8, 2, 5, 2, 6, 3, 2, 8, -1, -1, -1, -1, -1, -1,
			2, 3, 11, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			11, 0, 8, 11, 2, 0, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			0, 1, 9, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			5, 10, 6, 1, 9, 2, 9, 11, 2, 9, 8, 11, -1, -1, -1, -1, -1, -1,
			6, 3, 11, 6, 5, 3, 5, 1, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			0, 8, 11, 0, 11, 5, 0, 5, 1, 5, 11, 6, -1, -1, -1, -1, -1, -1,
			3, 11, 6, 0, 3, 6, 0, 6, 5, 0, 5, 9, -1, -1, -1, -1, -1, -1,
			6, 5, 9, 6, 9, 11, 11, 9, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			5, 10, 6, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			4, 3, 0, 4, 7, 3, 6, 5, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			1, 9, 0, 5, 10, 6, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			10, 6, 5, 1, 9, 7, 1, 7, 3, 7, 9, 4, -1, -1, -1, -1, -1, -1,
			6, 1, 2, 6, 5, 1, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			1, 2, 5, 5, 2, 6, 3, 0, 4, 3, 4, 7, -1, -1, -1, -1, -1, -1,
			8, 4, 7, 9, 0, 5, 0, 6, 5, 0, 2, 6, -1, -1, -1, -1, -1, -1,
			7, 3, 9, 7, 9, 4, 3, 2, 9, 5, 9, 6, 2, 6, 9, -1, -1, -1,
			3, 11, 2, 7, 8, 4, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			5, 10, 6, 4, 7, 2, 4, 2, 0, 2, 7, 11, -1, -1, -1, -1, -1, -1,
			0, 1, 9, 4, 7, 8, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1, -1, -1,
			9, 2, 1, 9, 11, 2, 9, 4, 11, 7, 11, 4, 5, 10, 6, -1, -1, -1,
			8, 4, 7, 3, 11, 5, 3, 5, 1, 5, 11, 6, -1, -1, -1, -1, -1, -1,
			5, 1, 11, 5, 11, 6, 1, 0, 11, 7, 11, 4, 0, 4, 11, -1, -1, -1,
			0, 5, 9, 0, 6, 5, 0, 3, 6, 11, 6, 3, 8, 4, 7, -1, -1, -1,
			6, 5, 9, 6, 9, 11, 4, 7, 9, 7, 11, 9, -1, -1, -1, -1, -1, -1,
			10, 4, 9, 6, 4, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			4, 10, 6, 4, 9, 10, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			10, 0, 1, 10, 6, 0, 6, 4, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			8, 3, 1, 8, 1, 6, 8, 6, 4, 6, 1, 10, -1, -1, -1, -1, -1, -1,
			1, 4, 9, 1, 2, 4, 2, 6, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			3, 0, 8, 1, 2, 9, 2, 4, 9, 2, 6, 4, -1, -1, -1, -1, -1, -1,
			0, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			8, 3, 2, 8, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			10, 4, 9, 10, 6, 4, 11, 2, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			0, 8, 2, 2, 8, 11, 4, 9, 10, 4, 10, 6, -1, -1, -1, -1, -1, -1,
			3, 11, 2, 0, 1, 6, 0, 6, 4, 6, 1, 10, -1, -1, -1, -1, -1, -1,
			6, 4, 1, 6, 1, 10, 4, 8, 1, 2, 1, 11, 8, 11, 1, -1, -1, -1,
			9, 6, 4, 9, 3, 6, 9, 1, 3, 11, 6, 3, -1, -1, -1, -1, -1, -1,
			8, 11, 1, 8, 1, 0, 11, 6, 1, 9, 1, 4, 6, 4, 1, -1, -1, -1,
			3, 11, 6, 3, 6, 0, 0, 6, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			6, 4, 8, 11, 6, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			7, 10, 6, 7, 8, 10, 8, 9, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			0, 7, 3, 0, 10, 7, 0, 9, 10, 6, 7, 10, -1, -1, -1, -1, -1, -1,
			10, 6, 7, 1, 10, 7, 1, 7, 8, 1, 8, 0, -1, -1, -1, -1, -1, -1,
			10, 6, 7, 10, 7, 1, 1, 7, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			1, 2, 6, 1, 6, 8, 1, 8, 9, 8, 6, 7, -1, -1, -1, -1, -1, -1,
			2, 6, 9, 2, 9, 1, 6, 7, 9, 0, 9, 3, 7, 3, 9, -1, -1, -1,
			7, 8, 0, 7, 0, 6, 6, 0, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			7, 3, 2, 6, 7, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			2, 3, 11, 10, 6, 8, 10, 8, 9, 8, 6, 7, -1, -1, -1, -1, -1, -1,
			2, 0, 7, 2, 7, 11, 0, 9, 7, 6, 7, 10, 9, 10, 7, -1, -1, -1,
			1, 8, 0, 1, 7, 8, 1, 10, 7, 6, 7, 10, 2, 3, 11, -1, -1, -1,
			11, 2, 1, 11, 1, 7, 10, 6, 1, 6, 7, 1, -1, -1, -1, -1, -1, -1,
			8, 9, 6, 8, 6, 7, 9, 1, 6, 11, 6, 3, 1, 3, 6, -1, -1, -1,
//		0, 9, 1, 11, 6, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1,-1, -1, -1,
			11, 1, 0, 6, 9, 1, 11, 6, 1, 6, 7, 9, 9, 7, 0, 7, 11, 0,
			7, 8, 0, 7, 0, 6, 3, 11, 0, 11, 6, 0, -1, -1, -1, -1, -1, -1,
			7, 11, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			3, 0, 8, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			0, 1, 9, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			8, 1, 9, 8, 3, 1, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			10, 1, 2, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			1, 2, 10, 3, 0, 8, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			2, 9, 0, 2, 10, 9, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			6, 11, 7, 2, 10, 3, 10, 8, 3, 10, 9, 8, -1, -1, -1, -1, -1, -1,
			7, 2, 3, 6, 2, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			7, 0, 8, 7, 6, 0, 6, 2, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			2, 7, 6, 2, 3, 7, 0, 1, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			1, 6, 2, 1, 8, 6, 1, 9, 8, 8, 7, 6, -1, -1, -1, -1, -1, -1,
			10, 7, 6, 10, 1, 7, 1, 3, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			10, 7, 6, 1, 7, 10, 1, 8, 7, 1, 0, 8, -1, -1, -1, -1, -1, -1,
			0, 3, 7, 0, 7, 10, 0, 10, 9, 6, 10, 7, -1, -1, -1, -1, -1, -1,
			7, 6, 10, 7, 10, 8, 8, 10, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			6, 8, 4, 11, 8, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			3, 6, 11, 3, 0, 6, 0, 4, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			8, 6, 11, 8, 4, 6, 9, 0, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			9, 4, 6, 9, 6, 3, 9, 3, 1, 11, 3, 6, -1, -1, -1, -1, -1, -1,
			6, 8, 4, 6, 11, 8, 2, 10, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			1, 2, 10, 3, 0, 11, 0, 6, 11, 0, 4, 6, -1, -1, -1, -1, -1, -1,
			4, 11, 8, 4, 6, 11, 0, 2, 9, 2, 10, 9, -1, -1, -1, -1, -1, -1,
			10, 9, 3, 10, 3, 2, 9, 4, 3, 11, 3, 6, 4, 6, 3, -1, -1, -1,
			8, 2, 3, 8, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			0, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			1, 9, 0, 2, 3, 4, 2, 4, 6, 4, 3, 8, -1, -1, -1, -1, -1, -1,
			1, 9, 4, 1, 4, 2, 2, 4, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			8, 1, 3, 8, 6, 1, 8, 4, 6, 6, 10, 1, -1, -1, -1, -1, -1, -1,
			10, 1, 0, 10, 0, 6, 6, 0, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			4, 6, 3, 4, 3, 8, 6, 10, 3, 0, 3, 9, 10, 9, 3, -1, -1, -1,
			10, 9, 4, 6, 10, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			4, 9, 5, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			0, 8, 3, 4, 9, 5, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			5, 0, 1, 5, 4, 0, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			11, 7, 6, 8, 3, 4, 3, 5, 4, 3, 1, 5, -1, -1, -1, -1, -1, -1,
			9, 5, 4, 10, 1, 2, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			6, 11, 7, 1, 2, 10, 0, 8, 3, 4, 9, 5, -1, -1, -1, -1, -1, -1,
			7, 6, 11, 5, 4, 10, 4, 2, 10, 4, 0, 2, -1, -1, -1, -1, -1, -1,
			3, 4, 8, 3, 5, 4, 3, 2, 5, 10, 5, 2, 11, 7, 6, -1, -1, -1,
			7, 2, 3, 7, 6, 2, 5, 4, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			9, 5, 4, 0, 8, 6, 0, 6, 2, 6, 8, 7, -1, -1, -1, -1, -1, -1,
			3, 6, 2, 3, 7, 6, 1, 5, 0, 5, 4, 0, -1, -1, -1, -1, -1, -1,
			6, 2, 8, 6, 8, 7, 2, 1, 8, 4, 8, 5, 1, 5, 8, -1, -1, -1,
			9, 5, 4, 10, 1, 6, 1, 7, 6, 1, 3, 7, -1, -1, -1, -1, -1, -1,
			1, 6, 10, 1, 7, 6, 1, 0, 7, 8, 7, 0, 9, 5, 4, -1, -1, -1,
			4, 0, 10, 4, 10, 5, 0, 3, 10, 6, 10, 7, 3, 7, 10, -1, -1, -1,
			7, 6, 10, 7, 10, 8, 5, 4, 10, 4, 8, 10, -1, -1, -1, -1, -1, -1,
			6, 9, 5, 6, 11, 9, 11, 8, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			3, 6, 11, 0, 6, 3, 0, 5, 6, 0, 9, 5, -1, -1, -1, -1, -1, -1,
			0, 11, 8, 0, 5, 11, 0, 1, 5, 5, 6, 11, -1, -1, -1, -1, -1, -1,
			6, 11, 3, 6, 3, 5, 5, 3, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			1, 2, 10, 9, 5, 11, 9, 11, 8, 11, 5, 6, -1, -1, -1, -1, -1, -1,
			0, 11, 3, 0, 6, 11, 0, 9, 6, 5, 6, 9, 1, 2, 10, -1, -1, -1,
			11, 8, 5, 11, 5, 6, 8, 0, 5, 10, 5, 2, 0, 2, 5, -1, -1, -1,
			6, 11, 3, 6, 3, 5, 2, 10, 3, 10, 5, 3, -1, -1, -1, -1, -1, -1,
			5, 8, 9, 5, 2, 8, 5, 6, 2, 3, 8, 2, -1, -1, -1, -1, -1, -1,
			9, 5, 6, 9, 6, 0, 0, 6, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			1, 5, 8, 1, 8, 0, 5, 6, 8, 3, 8, 2, 6, 2, 8, -1, -1, -1,
			1, 5, 6, 2, 1, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			1, 3, 6, 1, 6, 10, 3, 8, 6, 5, 6, 9, 8, 9, 6, -1, -1, -1,
			10, 1, 0, 10, 0, 6, 9, 5, 0, 5, 6, 0, -1, -1, -1, -1, -1, -1,
//      0, 3, 8, 5, 6, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1,-1, -1, -1,
			5, 8, 0, 5, 6, 8, 6, 3, 8, 6, 10, 3, 3, 10, 0, 10, 5, 0,
			10, 5, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			11, 5, 10, 7, 5, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			11, 5, 10, 11, 7, 5, 8, 3, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			5, 11, 7, 5, 10, 11, 1, 9, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			10, 7, 5, 10, 11, 7, 9, 8, 1, 8, 3, 1, -1, -1, -1, -1, -1, -1,
			11, 1, 2, 11, 7, 1, 7, 5, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			0, 8, 3, 1, 2, 7, 1, 7, 5, 7, 2, 11, -1, -1, -1, -1, -1, -1,
			9, 7, 5, 9, 2, 7, 9, 0, 2, 2, 11, 7, -1, -1, -1, -1, -1, -1,
			7, 5, 2, 7, 2, 11, 5, 9, 2, 3, 2, 8, 9, 8, 2, -1, -1, -1,
			2, 5, 10, 2, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			8, 2, 0, 8, 5, 2, 8, 7, 5, 10, 2, 5, -1, -1, -1, -1, -1, -1,
			9, 0, 1, 5, 10, 3, 5, 3, 7, 3, 10, 2, -1, -1, -1, -1, -1, -1,
			9, 8, 2, 9, 2, 1, 8, 7, 2, 10, 2, 5, 7, 5, 2, -1, -1, -1,
			1, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			0, 8, 7, 0, 7, 1, 1, 7, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			9, 0, 3, 9, 3, 5, 5, 3, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			9, 8, 7, 5, 9, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			5, 8, 4, 5, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			5, 0, 4, 5, 11, 0, 5, 10, 11, 11, 3, 0, -1, -1, -1, -1, -1, -1,
			0, 1, 9, 8, 4, 10, 8, 10, 11, 10, 4, 5, -1, -1, -1, -1, -1, -1,
			10, 11, 4, 10, 4, 5, 11, 3, 4, 9, 4, 1, 3, 1, 4, -1, -1, -1,
			2, 5, 1, 2, 8, 5, 2, 11, 8, 4, 5, 8, -1, -1, -1, -1, -1, -1,
			0, 4, 11, 0, 11, 3, 4, 5, 11, 2, 11, 1, 5, 1, 11, -1, -1, -1,
			0, 2, 5, 0, 5, 9, 2, 11, 5, 4, 5, 8, 11, 8, 5, -1, -1, -1,
//		9, 4, 5, 2, 11, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1,-1, -1, -1,
			5, 9, 2, 11, 5, 2, 4, 5, 11, 3, 4, 11, 9, 4, 3, 9, 3, 2,
			2, 5, 10, 3, 5, 2, 3, 4, 5, 3, 8, 4, -1, -1, -1, -1, -1, -1,
			5, 10, 2, 5, 2, 4, 4, 2, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			3, 10, 2, 3, 5, 10, 3, 8, 5, 4, 5, 8, 0, 1, 9, -1, -1, -1,
			5, 10, 2, 5, 2, 4, 1, 9, 2, 9, 4, 2, -1, -1, -1, -1, -1, -1,
			8, 4, 5, 8, 5, 3, 3, 5, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			0, 4, 5, 1, 0, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			8, 4, 5, 8, 5, 3, 9, 0, 5, 0, 3, 5, -1, -1, -1, -1, -1, -1,
			9, 4, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			4, 11, 7, 4, 9, 11, 9, 10, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			0, 8, 3, 4, 9, 7, 9, 11, 7, 9, 10, 11, -1, -1, -1, -1, -1, -1,
			1, 10, 11, 1, 11, 4, 1, 4, 0, 7, 4, 11, -1, -1, -1, -1, -1, -1,
			3, 1, 4, 3, 4, 8, 1, 10, 4, 7, 4, 11, 10, 11, 4, -1, -1, -1,
			4, 11, 7, 9, 11, 4, 9, 2, 11, 9, 1, 2, -1, -1, -1, -1, -1, -1,
			9, 7, 4, 9, 11, 7, 9, 1, 11, 2, 11, 1, 0, 8, 3, -1, -1, -1,
			11, 7, 4, 11, 4, 2, 2, 4, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			11, 7, 4, 11, 4, 2, 8, 3, 4, 3, 2, 4, -1, -1, -1, -1, -1, -1,
			2, 9, 10, 2, 7, 9, 2, 3, 7, 7, 4, 9, -1, -1, -1, -1, -1, -1,
			9, 10, 7, 9, 7, 4, 10, 2, 7, 8, 7, 0, 2, 0, 7, -1, -1, -1,
			3, 7, 10, 3, 10, 2, 7, 4, 10, 1, 10, 0, 4, 0, 10, -1, -1, -1,
//		1, 10, 2, 8, 7, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1,-1, -1, -1,
			10, 4, 1, 4, 8, 1, 8, 2, 1, 8, 7, 2, 7, 10, 2, 7, 4, 10,
			4, 9, 1, 4, 1, 7, 7, 1, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			4, 9, 1, 4, 1, 7, 0, 8, 1, 8, 7, 1, -1, -1, -1, -1, -1, -1,
			4, 0, 3, 7, 4, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			4, 8, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			9, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			3, 0, 9, 3, 9, 11, 11, 9, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			0, 1, 10, 0, 10, 8, 8, 10, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			3, 1, 10, 11, 3, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			1, 2, 11, 1, 11, 9, 9, 11, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			3, 0, 9, 3, 9, 11, 1, 2, 9, 2, 11, 9, -1, -1, -1, -1, -1, -1,
			0, 2, 11, 8, 0, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			3, 2, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			2, 3, 8, 2, 8, 10, 10, 8, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			9, 10, 2, 0, 9, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			2, 3, 8, 2, 8, 10, 0, 1, 8, 1, 10, 8, -1, -1, -1, -1, -1, -1,
			1, 10, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			1, 3, 8, 9, 1, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			0, 9, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			0, 3, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1
		};
	}
}