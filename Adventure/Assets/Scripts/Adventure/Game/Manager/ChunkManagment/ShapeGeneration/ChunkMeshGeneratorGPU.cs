using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adventure.Game.Manager.ChunkManagment;
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
		private ChunkRemeshListener OnChunkRemeshLod;

		private int solid, liquid, solidLod, bakeSolid, bakeLiquid;
		private GraphicsBuffer triangleTable, triangleTransitionTable;

		public ChunkMeshGeneratorGPU(ComputeShader shader, ChunkRemeshListener chunkRemeshListener, ChunkRemeshListener chunkRemeshLodListener) : base(chunkRemeshListener) {
			this.shader = shader;
			this.OnChunkRemesh = chunkRemeshListener;
			this.OnChunkRemeshLod = chunkRemeshLodListener;
			this.voxels = new Vector2[OFFSIZE * OFFSIZE * OFFSIZE];
			this.adjacentChunks = new Chunk[27];
		}

		public override void Init() {
			solid = shader.FindKernel("Marche");
			solidLod = shader.FindKernel("MarcheLod");
			liquid = shader.FindKernel("Swim");
			bakeSolid = shader.FindKernel("BakeSolid");
			bakeLiquid = shader.FindKernel("BakeLiquid");

			triangleTable = new GraphicsBuffer(GraphicsBuffer.Target.Structured, TriangleConnectionTable.Length, sizeof(int));
			triangleTransitionTable = new GraphicsBuffer(GraphicsBuffer.Target.Structured, TriangleTransitionConnectionTable.Length, sizeof(int));
			voxelBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, OFFSIZE * OFFSIZE * OFFSIZE, sizeof(float) * 2);

			vertexSolid = new GraphicsBuffer(GraphicsBuffer.Target.Structured, CHUNK_MAX_VERTEX, sizeof(float) * (3 + 3 + 4));
			counterSolid = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(int));

			vertexLiquid = new GraphicsBuffer(GraphicsBuffer.Target.Structured, CHUNK_MAX_VERTEX, sizeof(float) * (3 + 3 + 4));
			counterLiquid = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(int));

			shader.SetBuffer(solid, "TriangleTable", triangleTable);
			shader.SetBuffer(solid, "VoxelBuffer", voxelBuffer);
			shader.SetBuffer(solid, "VertexSolid", vertexSolid);
			shader.SetBuffer(solid, "CounterSolid", counterSolid);

			shader.SetBuffer(solidLod, "TriangleTable", triangleTransitionTable);
			shader.SetBuffer(solidLod, "VoxelBuffer", voxelBuffer);
			shader.SetBuffer(solidLod, "VertexSolid", vertexSolid);
			shader.SetBuffer(solidLod, "CounterSolid", counterSolid);
			
			shader.SetBuffer(bakeSolid, "VertexSolid", vertexSolid);

			shader.SetBuffer(liquid, "TriangleTable", triangleTable);
			shader.SetBuffer(liquid, "VoxelBuffer", voxelBuffer);
			shader.SetBuffer(liquid, "VertexLiquid", vertexLiquid);
			shader.SetBuffer(liquid, "CounterLiquid", counterLiquid);

			shader.SetBuffer(bakeLiquid, "VertexLiquid", vertexLiquid);

			triangleTable.SetData(TriangleConnectionTable);
			triangleTransitionTable.SetData(TriangleTransitionConnectionTable);
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

		public override bool RemeshChunkLod(Chunk chunk, WorldSettings settings, Dictionary<Vector3Int, ChunkHolder> chunks, Vector3Int minLimit, Vector3Int maxLimit) {
			if (CreateDerivedChunk(voxels, chunk, settings, chunks)) {
				this.currentChunk = chunk;
				this.isRemeshing = true;
				
				voxelBuffer.SetData(voxels);
				counterSolid.SetData(new uint[] { 0 });
				//counterLiquid.SetData(new uint[]{0});
				shader.SetFloat("lodDis", 0.01f);
				shader.SetVector("minlimit", new Vector4(minLimit.x, minLimit.y, minLimit.z, 0));
				shader.SetVector("maxLimit", new Vector4(maxLimit.x, maxLimit.y, maxLimit.z, 0));

				if (maxLimit.x == 1) {
					shader.SetVector("lodSideX", new Vector4(0, 1, 0));
					shader.SetVector("lodSideY", new Vector4(0, 0, 1));
					shader.SetVector("lodSideZ", new Vector4(1, 0, 0));
					shader.Dispatch(solidLod, 1, 1, 1);
				}
				if (minLimit.x == 1) {
					shader.SetVector("lodSideX", new Vector4(0, 0, 1));
					shader.SetVector("lodSideY", new Vector4(0, 1, 0));
					shader.SetVector("lodSideZ", new Vector4(-1, 0, 0));
					shader.Dispatch(solidLod, 1, 1, 1);
				}
				if (minLimit.y == 1) {
					shader.SetVector("lodSideX", new Vector4(1, 0, 0));
					shader.SetVector("lodSideY", new Vector4(0, 0, 1));
					shader.SetVector("lodSideZ", new Vector4(0, -1, 0));
					shader.Dispatch(solidLod, 1, 1, 1);
				}
				if (maxLimit.y == 1) {
					shader.SetVector("lodSideX", new Vector4(0, 0, 1));
					shader.SetVector("lodSideY", new Vector4(1, 0, 0));
					shader.SetVector("lodSideZ", new Vector4(0, 1, 0));
					shader.Dispatch(solidLod, 1, 1, 1);
				}
				if (minLimit.z == 1) {
					shader.SetVector("lodSideX", new Vector4(0, 1, 0));
					shader.SetVector("lodSideY", new Vector4(1, 0, 0));
					shader.SetVector("lodSideZ", new Vector4(0, 0, -1));
					shader.Dispatch(solidLod, 1, 1, 1);
				}
				if (maxLimit.z == 1) {
					shader.SetVector("lodSideX", new Vector4(1, 0, 0));
					shader.SetVector("lodSideY", new Vector4(0, 1, 0));
					shader.SetVector("lodSideZ", new Vector4(0, 0, 1));
					shader.Dispatch(solidLod, 1, 1, 1);
				}

				AsyncGPUReadback.Request(counterSolid, OnSolidLodReady);
				return true;
			} else {
				return false;
			}
		}
		
		private void OnSolidReady(AsyncGPUReadbackRequest request) {
			var data = request.GetData<uint>();
			indexCount = (int)data[0];
			
			Mesh mesh = indexCount > 0 ? ComposeMesh(indexCount) : null;
			isRemeshing = false;
			indexCount = 0;

			OnChunkRemesh?.Invoke(currentChunk, mesh);
		}
		
		private void OnSolidLodReady(AsyncGPUReadbackRequest request) {
			var data = request.GetData<uint>();
			indexCount = (int)data[0];
			
			Mesh mesh = indexCount > 0 ? ComposeMesh(indexCount) : null;
			isRemeshing = false;
			indexCount = 0;

			OnChunkRemeshLod?.Invoke(currentChunk, mesh);
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