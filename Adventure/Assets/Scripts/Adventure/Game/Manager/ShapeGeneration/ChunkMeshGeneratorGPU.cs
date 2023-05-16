using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adventure.Logic;
using Adventure.Logic.Data;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Adventure.Game.Manager.ShapeGeneration {
	public class ChunkMeshGeneratorGPU : ChunkMeshGenerator {

		// Source
		private GraphicsBuffer voxelBuffer;

		// Destination
		private GraphicsBuffer vertexSolid;
		private GraphicsBuffer counterSolid;

		private GraphicsBuffer vertexLiquid;
		private GraphicsBuffer counterLiquid;

		private ComputeShader shader;
		private ChunkRemeshListener OnChunkRemesh;

		private int solid, liquid, bakeSolid, bakeLiquid;
		private GraphicsBuffer triangleTable;

		public ChunkMeshGeneratorGPU(ComputeShader shader, ChunkRemeshListener chunkRemeshListener) : base(chunkRemeshListener) {
			this.shader = shader;
			this.OnChunkRemesh = chunkRemeshListener;
			this.voxels = new Vector2[OFFSIZE * OFFSIZE * OFFSIZE];
			this.adjacentChunks = new Chunk[27];
		}

		public override void Init() {
			solid = shader.FindKernel("Marche");
			liquid = shader.FindKernel("Swim");
			bakeSolid = shader.FindKernel("BakeSolid");
			bakeLiquid = shader.FindKernel("BakeLiquid");

			triangleTable = new GraphicsBuffer(GraphicsBuffer.Target.Structured, TriangleConnectionTable.Length, sizeof(int));
			voxelBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, OFFSIZE * OFFSIZE * OFFSIZE, sizeof(float) * 2);

			vertexSolid = new GraphicsBuffer(GraphicsBuffer.Target.Structured, CHUNK_MAX_VERTEX, sizeof(float) * (3 + 3 + 4));
			counterSolid = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(int));

			vertexLiquid = new GraphicsBuffer(GraphicsBuffer.Target.Structured, CHUNK_MAX_VERTEX, sizeof(float) * (3 + 3 + 4));
			counterLiquid = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(int));

			shader.SetBuffer(solid, "TriangleTable", triangleTable);
			shader.SetBuffer(solid, "VoxelBuffer", voxelBuffer);
			shader.SetBuffer(solid, "VertexSolid", vertexSolid);
			shader.SetBuffer(solid, "CounterSolid", counterSolid);

			shader.SetBuffer(bakeSolid, "VertexSolid", vertexSolid);

			shader.SetBuffer(liquid, "TriangleTable", triangleTable);
			shader.SetBuffer(liquid, "VoxelBuffer", voxelBuffer);
			shader.SetBuffer(liquid, "VertexLiquid", vertexLiquid);
			shader.SetBuffer(liquid, "CounterLiquid", counterLiquid);

			shader.SetBuffer(bakeLiquid, "VertexLiquid", vertexLiquid);

			triangleTable.SetData(TriangleConnectionTable);
		}

		public override void Release() {
			triangleTable.Release();
			voxelBuffer.Release();
			vertexSolid.Release();
			counterSolid.Release();
			vertexLiquid.Release();
			counterLiquid.Release();
			OnChunkRemesh = null;
		}

		public override bool RemeshChunk(Chunk chunk, WorldSettings settings, Dictionary<Vector3Int, ChunkHolder> chunks) {
			if (CreateDerivedChunk(voxels, chunk, settings, chunks)) {
				this.currentChunk = chunk;
				this.isRemeshing = true;
				
				voxelBuffer.SetData(voxels);
				counterSolid.SetData(new uint[] { 0 });
				//counterLiquid.SetData(new uint[]{0});

				shader.Dispatch(solid, 4, 4, 4);
				//shader.Dispatch(liquid, x/8, y/8, z/8);

				AsyncGPUReadback.Request(counterSolid, OnSolidReady);
				return true;
			} else {
				return false;
			}
		}

		private void OnSolidReady(AsyncGPUReadbackRequest request) {
			var data = request.GetData<uint>();
			indexCount = (int)data[0];

			if (indexCount > 0) {
				OnChunkRemesh?.Invoke(currentChunk, ComposeMesh(indexCount));
			} else {
				OnChunkRemesh?.Invoke(currentChunk, null);
			}

			isRemeshing = false;
			indexCount = 0;
		}

		private Mesh ComposeMesh(int vertexCount) {
			Mesh mesh = new Mesh();
			mesh.indexFormat = IndexFormat.UInt16;
			mesh.indexBufferTarget = GraphicsBuffer.Target.Structured;
			mesh.SetVertexBufferParams(vertexCount,
				new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float16, 4),
				new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float16, 4),
				new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 4)
			);
			mesh.SetIndexBufferParams(vertexCount, IndexFormat.UInt16);
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
			var arr = new GeneratedVertexLow[vertexCount];
			vertexSolid.GetData(arr);
			var ind = new int[vertexCount];
			for (int i = 0; i < vertexCount; i++) {
				ind[i] = i;
			}
			Vector3[] pos = new Vector3[vertexCount];
			Vector3[] nor = new Vector3[vertexCount];
			Vector4[] tex0 = new Vector4[vertexCount];
			for (int i = 0; i < vertexCount; i++) {
				pos[i] = new Vector3(arr[i].position.x, arr[i].position.y, arr[i].position.z);
				nor[i] = new Vector3(arr[i].normal.x, arr[i].normal.y, arr[i].normal.z);
				tex0[i] = new Vector4(arr[i].uv0.x, arr[i].uv0.y, arr[i].uv0.z, arr[i].uv0.w);
			}
			mesh.vertices = pos;
			mesh.normals = nor;
			mesh.triangles = ind;
			mesh.SetUVs(0, tex0);
			mesh.bounds = new Bounds(new Vector3(8, 8, 8), new Vector3(16, 16, 16));
			mesh.Optimize();
			return mesh;
		}

		public static void MeshToCpu(Mesh mesh) {
			var arr = new GeneratedVertexLow[mesh.vertexCount];
			mesh.GetVertexBuffer(0).GetData(arr);
			Vector3[] pos = new Vector3[mesh.vertexCount];
			Vector3[] nor = new Vector3[mesh.vertexCount];
			Vector4[] tex0 = new Vector4[mesh.vertexCount];
			for (int i = 0; i < mesh.vertexCount; i++) {
				pos[i] = new Vector3(arr[i].position.x, arr[i].position.y, arr[i].position.z);
				nor[i] = new Vector3(arr[i].normal.x, arr[i].normal.y, arr[i].normal.z);
				tex0[i] = new Vector4(arr[i].uv0.x, arr[i].uv0.y, arr[i].uv0.z, arr[i].uv0.w);
			}

			mesh.vertices = pos;
			mesh.normals = nor;
			mesh.SetUVs(0, tex0);
		}
	}
}