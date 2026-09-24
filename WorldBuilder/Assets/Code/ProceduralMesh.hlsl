#ifndef GENERATED_VERTEX_INCLUDED
#define GENERATED_VERTEX_INCLUDED

struct GeneratedVertex {
    uint2 data0;
    uint2 data1;
};

struct ChunkPos {
    uint xy;
    uint z;
    uint lod;
    uint offset;
};

StructuredBuffer<GeneratedVertex> MeshVertexBuffer;
StructuredBuffer<ChunkPos> ChunkPosBuffer;

float DecodePosition(uint v) {
    return float(v) * (32.0 / 65535.0);
}

#ifndef UNITY_INDIRECT_DRAW_ARGS
#define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
#include "UnityIndirect.cginc"
#endif

#ifndef RENDER_MESH_INDIRECT_EXAMPLE
#define RENDER_MESH_INDIRECT_EXAMPLE

void IndirectCommandID(out uint InstanceID)
{
    #ifndef SHADERGRAPH_PREVIEW
        InitIndirectDrawArgs(0);
        InstanceID = GetCommandID(0);
    #else
        CommandID = 0;
        IndirectInstanceCount = 1;
    #endif
}
#endif

void GetGeneratedVertex_float(
    uint VertexID,
    out float3 Position,
    out float3 Normal,
    out float4 UV0,
    out float4 UV1) {
    
    uint instanceID;
    IndirectCommandID(instanceID);

    const ChunkPos chunkPos = ChunkPosBuffer[instanceID];
    
    const uint vtxId = (uint)chunkPos.offset + VertexID;
    const GeneratedVertex v = MeshVertexBuffer[vtxId];

   const uint x = chunkPos.xy & 0xFFFF;
   const uint y = chunkPos.xy >> 16;
   const uint z = chunkPos.z;
   const uint lod = chunkPos.lod;
    
    Position = float3(
        DecodePosition(v.data0.x & 0xffff),
        DecodePosition(v.data0.x >> 16),
        DecodePosition(v.data0.y & 0xffff)
    ) * lod + float3(x, y, z);

    Normal = float3(
        f16tof32(v.data1.x & 0xffff),
        f16tof32(v.data1.x >> 16),
        f16tof32(v.data1.y & 0xffff)
    );

    const uint uv0x = (v.data0.y >> 16) & 0x3F;
    const uint uv0y = (v.data0.y >> 22) & 0x3F;
    const uint uv0z = (v.data1.y >> 16) & 0x3F;

    UV0 = float4(uv0x, uv0y, uv0z, (uv0x == uv0y && uv0y == uv0z) ? 0.0 : 1.0);
    
    const uint local = vtxId % 3;
    
    UV1 = float4(
        local == 0 ? 1.0 : 0.0,
        local == 1 ? 1.0 : 0.0,
        local == 2 ? 1.0 : 0.0,
        0.0
    );
}

#endif