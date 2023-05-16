using System;
using System.Collections;
using System.Collections.Generic;
using Adventure.Logic;
using Adventure.Logic.Data;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Adventure.Game.Manager.ShapeGeneration {
    public class ChunkMeshGeneratorCPU : ChunkMeshGenerator {
        
        public ChunkMeshGeneratorCPU(ChunkRemeshListener chunkRemeshListener) : base(chunkRemeshListener) {
        }

        public override bool RemeshChunk(Chunk chunk, WorldSettings settings, Dictionary<Vector3Int, ChunkHolder> chunks) {
            if (CreateDerivedChunk(voxels, chunk, settings, chunks)) {
                this.currentChunk = chunk;
                this.isRemeshing = true;
                GameServer.StartAsync(Dispatch());
                return true;
            } else {
                return false;
            }
        }

        public IEnumerator Dispatch() {
            float time = Time.realtimeSinceStartup;
            for (int z = 0; z < 16; z++) {
                for (int y = 0; y < 16; y++) {
                    for (int x = 0; x < 16; x++) {
                        Marche(new int3(x, y, z));
                    }
                }

                if (Time.realtimeSinceStartup - time > 1 / 60f) {
                    yield return true;
                    
                    time = Time.realtimeSinceStartup;
                }
            }

            OnSolidReady();
        }


        private void OnSolidReady() {
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

            meshVertex.SetData(vertexSolid, 0, 0, vertexCount);
            meshIndex.SetData(indices, 0, 0, vertexCount);

            return mesh;
        }

        protected ushort[] indices = new ushort[CHUNK_MAX_VERTEX];
        protected GeneratedVertexLow[] vertexSolid = new GeneratedVertexLow[CHUNK_MAX_VERTEX];
        protected GeneratedVertexLow[] verticesLiquid = new GeneratedVertexLow[CHUNK_MAX_VERTEX];

        private const int SIZE = 20;
        private const int SIZE2 = 400;
        private const int SIZE3 = 8000;
        private const float NORMD = 0.025f;
        
        float2 GetVox(int x, int y, int z) {
            return voxels[x + 2 + (y + 2) * SIZE + (z + 2) * SIZE2];
        }

        float2 SampleSolid(int3 pos) {
            float2 vox = GetVox(pos.x, pos.y, pos.z);
            return new float2(math.max(math.sign(vox.y + 1), 0) * vox.x, vox.y);
        }
            
        float SampleVoxel(int3 pos) {
            float2 vox = GetVox(pos.x, pos.y, pos.z);
            return math.max(math.sign(vox.y + 1), 0) * vox.x;
        }
        
        float3 GetSurfaceNormal(float3 pos) {
            int3 ibase = new int3(math.floor(pos));
            float fra = math.length(pos - ibase);
            int3 si0 = new int3(math.ceil(pos - ibase));
            int3 si1 = new int3(si0.z, si0.x, si0.y);
            int3 si2 = new int3(si0.y, si0.z, si0.x);
    
            float coord_a0 = SampleVoxel(ibase);
            float coord_a1 = SampleVoxel(ibase + si0);

            float coord_b0 = SampleVoxel(ibase + si1);
            float coord_b1 = SampleVoxel(ibase + si1 + si0);
            float coord_b2 = SampleVoxel(ibase - si1);
            float coord_b3 = SampleVoxel(ibase - si1 + si0);

            float coord_c0 = SampleVoxel(ibase + si2);
            float coord_c1 = SampleVoxel(ibase + si2 + si0);
            float coord_c2 = SampleVoxel(ibase - si2);
            float coord_c3 = SampleVoxel(ibase - si2 + si0);

            float lerp_a0 = math.lerp(coord_a0, coord_a1, fra); // The Center. Always 0.5 = On Surface
            float lerp_b0 = math.lerp(coord_b0, coord_b1, fra);
            float lerp_b2 = math.lerp(coord_b2, coord_b3, fra);
            float lerp_c0 = math.lerp(coord_c0, coord_c1, fra);
            float lerp_c2 = math.lerp(coord_c2, coord_c3, fra);

            float na0 = math.lerp(coord_a0, coord_a1, fra + NORMD) - .5f;
            float na2 = math.lerp(coord_a0, coord_a1, fra - NORMD) - .5f;
            float nb0 = math.lerp(lerp_a0, lerp_b0, NORMD) - .5f;
            float nb2 = math.lerp(lerp_a0, lerp_b2, NORMD) - .5f;
            float nc0 = math.lerp(lerp_a0, lerp_c0, NORMD) - .5f;
            float nc2 = math.lerp(lerp_a0, lerp_c2, NORMD) - .5f;

            float3 sa = new float3(si0) * (na0 - na2);
            float3 sb = new float3(si1) * (nb0 - nb2);
            float3 sc = new float3(si2) * (nc0 - nc2);
            return -math.normalize(sa + sb + sc);
        }
            
        void WriteVertex(int offset, float3 p, float3 n, float3 uv0, float uv1) {
            GeneratedVertexLow vertex;
            vertex.position = new half4((half)p.x, (half)p.y, (half)p.z, (half)1);
            vertex.normal = new half4((half)n.x, (half)n.y, (half)n.z, (half)1);
            vertex.uv0 = new half4((half)uv0.x, (half)uv0.y, (half)uv0.z, (half)uv1);
            vertexSolid[offset] = vertex;
            indices[offset] = (ushort)offset;
        }
            
        private static readonly int3[] VertexOffset ={
            new int3(0, 0, 0), new int3(1, 0, 0), new int3(1, 1, 0), new int3(0, 1, 0),
            new int3(0, 0, 1), new int3(1, 0, 1), new int3(1, 1, 1), new int3(0, 1, 1)
        };
        int3 CubeVertex(int index) {
            return VertexOffset[index];
        }
        
        private static readonly int2[] EdgeConnection = {
            new int2(0, 1), new int2(1, 2), new int2(2, 3), new int2(3, 0),
            new int2(4, 5), new int2(5, 6), new int2(6, 7), new int2(7, 4),
            new int2(0, 4), new int2(1, 5), new int2(2, 6), new int2(3, 7)
        };
        int2 EdgeVertexPair(int index) {
            return EdgeConnection[index];
        }
        
        private float2[] samples = new float2[8];
        private float3[] vertices = new float3[12];
        private float3[] normals = new float3[12];
        private float[] material = new float[12];
        
        private void Marche(int3 id) {
            int selector = 0;
            int i;
            for (i = 0; i < 8; i++) {
                samples[i] = SampleSolid(id + CubeVertex(i));
                selector |= (samples[i].x < 0.5 ? 1 : 0) << i;
            }
    
            if (selector == 0 || selector == 0xff) return;
    
            for (i = 0; i < 12; i++) {
                int2 pair = EdgeVertexPair(i);
                float2 sample1 = samples[pair.x];
                float2 sample2 = samples[pair.y];
                if (sample1.x < 0.5 == sample2.x < 0.5) continue;
                
                float3 vertex1 = id + CubeVertex(pair.x);
                float3 vertex2 = id + CubeVertex(pair.y);
                float param = (0.5f - sample1.x) / (sample2.x - sample1.x);
        
                vertices[i] = math.lerp(vertex1, vertex2, param);
                normals[i] = GetSurfaceNormal(vertices[i]);
                material[i] = math.abs(
                    sample1.x == 0 ? sample2.y :
                    sample2.x == 0 ? sample1.y :param < 0.5 ? sample1.y : sample2.y);

                if (math.abs(normals[i].x) > 0.99 && math.abs(normals[i].y) < 0.01 && math.abs(normals[i].z) < 0.01) {
                    normals[i].xyz = new float3(math.sign(normals[i].x) * 0.99f, 0.01f, 0.01f);
                }
            }

            int table_index = selector * 18;
            for (i = 0; i < 18; i+= 3) {
                int t_index0 = TriangleConnectionTable[table_index + i];
                int t_index1 = TriangleConnectionTable[table_index + i + 1];
                int t_index2 = TriangleConnectionTable[table_index + i + 2];
                if (t_index0 < 0) return;

                float3 uv0 = new float3(material[t_index0], material[t_index1], material[t_index2]);
                WriteVertex(indexCount++, vertices[t_index0], normals[t_index0], uv0, 1);
                WriteVertex(indexCount++, vertices[t_index1], normals[t_index1], uv0, 2);
                WriteVertex(indexCount++, vertices[t_index2], normals[t_index2], uv0, 3);
            }
        }
    }
}