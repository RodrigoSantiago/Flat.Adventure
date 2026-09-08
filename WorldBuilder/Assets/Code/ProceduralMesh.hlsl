#ifndef GENERATED_VERTEX_INCLUDED
#define GENERATED_VERTEX_INCLUDED

struct GeneratedVertex
{
    uint2 data0;
    uint2 data1;
};

StructuredBuffer<GeneratedVertex> MeshVertexBuffer;
float4 ChunkPos;

float4 UnpackHalf4(uint2 value)
{
    return float4(
        f16tof32(value.x),
        f16tof32(value.y),
        f16tof32(value.x >> 16),
        f16tof32(value.y >> 16)
    );
}

float DecodePosition(uint v)
{
    return float(v) * (32.0 / 65535.0);
}

void GetGeneratedVertex_float(
    uint VertexID,
    out float3 Position,
    out float3 Normal,
    out float4 UV0,
    out float4 UV1)
{
    GeneratedVertex v = MeshVertexBuffer[VertexID];

    Position = float3(
        DecodePosition(v.data0.x & 0xffff),
        DecodePosition(v.data0.x >> 16),
        DecodePosition(v.data0.y & 0xffff)
    ) * ChunkPos.w + ChunkPos.xyz;

    Normal = float3(
        f16tof32(v.data1.x & 0xffff),
        f16tof32(v.data1.x >> 16),
        f16tof32(v.data1.y & 0xffff)
    );

    const uint uv0x = (v.data0.y >> 16) & 0x3F;
    const uint uv0y = (v.data0.y >> 22) & 0x3F;
    const uint uv0z = (v.data1.y >> 16) & 0x3F;

    UV0 = float4(
        uv0x,
        uv0y,
        uv0z,
        (uv0x == uv0y && uv0y == uv0z) ? 0.0 : 1.0
    );
    
    const uint local = VertexID % 3;
    
    UV1 = float4(
        local == 0 ? 1.0 : 0.0,
        local == 1 ? 1.0 : 0.0,
        local == 2 ? 1.0 : 0.0,
        0.0
    );
}

#endif