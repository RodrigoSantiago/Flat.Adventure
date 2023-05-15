#ifndef BIPLANAR_INCLUDE
#define BIPLANAR_INCLUDE

void triplanarParallax_float(Texture2D textureSampler,  SamplerState stateA,Texture2D normalMapSampler,  SamplerState stateB,float3 position, float3 normal, float parallaxScale, float parallaxBias, out float4 result) {
    // Calculate the tangent and bitangent vectors
    float3 tangent = normalize(cross(normal, float3(0.0, 0.0, 1.0)));
    float3 bitangent = normalize(cross(normal, tangent));
    
    // Calculate the texture coordinates for the three planes
    float2 texCoordX = position.yz * parallaxScale + float2(position.x, 0.0);
    float2 texCoordY = position.xz * parallaxScale + float2(position.y, 0.0);
    float2 texCoordZ = position.xy * parallaxScale + float2(position.z, 0.0);
    
    // Calculate the heights of the three planes using the parallax mapping technique
    float heightX = textureSampler.SampleLevel(stateA, texCoordX, 0).r;
    float heightY = textureSampler.SampleLevel(stateA, texCoordY, 0).r;
    float heightZ = textureSampler.SampleLevel(stateA, texCoordZ, 0).r;
    
    // Calculate the offset for each plane using the heights and bias
    float3 offsetX = tangent * (heightX * parallaxScale - parallaxBias);
    float3 offsetY = bitangent * (heightY * parallaxScale - parallaxBias);
    float3 offsetZ = float3(0.0, 0.0, heightZ * parallaxScale - parallaxBias);
    
    // Apply the parallax offset to the world position
    position += offsetX + offsetY + offsetZ;
    
    // Calculate the final texture coordinate as the average of the three planes
    float2 texCoord = (texCoordX + texCoordY + texCoordZ) / 3.0;
    
    // Calculate the final color using the texture and normal map
    float4 color = textureSampler.SampleLevel(stateA, texCoord, 0);
    float3 normalMapColor = normalMapSampler.SampleLevel(stateB, texCoord, 0).rgb * 2.0 - 1.0;
    float3 normalDir = normalize(mul(normalMapColor, float3x3(tangent, bitangent, normal)));
    float diffuse = max(dot(normalDir, float3(0.0, 0.0, 1.0)), 0.0);
    result = float4(color.rgb * diffuse, color.a);
}

void biplanar_sampler_float(in float3 p, in float3 n,
                            out float2 uv0, out float2x2 uvd0, 
                            out float2 uv1, out float2x2 uvd1, out float t) {
    const uint3 value[] = {
        uint3(0, 2, 1),
        uint3(1, 2, 0),
        uint3(2, 0, 1)
    };
    const float3 dpdx = ddx(p);
    const float3 dpdy = ddy(p);
    const float3 pn = abs(n);
    const float3 sn = float3(sign(n.x), -sign(n.y), -sign(n.z));

    const uint largest = (pn.x >= pn.y && pn.x >= pn.z) ? 0 : (pn.y >= pn.z) ? 1 : 2;
    const uint smallest = (pn.z <= pn.y && pn.z <= pn.x) ? 2 : (pn.y <= pn.x) ? 1 : 0;
    const uint middle = (3 - largest - smallest);
    
    const uint3 ma = value[largest];
    const uint3 mi = value[smallest];
    const uint3 me = value[middle];
    
    const float z = pow(pn[mi.x] / 0.58, 8) * 4;
    float2 m = normalize(float2(pn[ma.x], pn[me.x]));
    m = clamp((m - 0.5773) / (1.0 - 0.5773), 0.0, 1.0);
    m = pow(m * m, z + 2);
    
    uv0 = float2(p[ma.y] * sn[ma.x], p[ma.z]);
    uv1 = float2(p[me.y] * sn[me.x], p[me.z]);
    uvd0 = float2x2(
            dpdx[ma.y] * sn[ma.x], dpdx[ma.z],
            dpdy[ma.y] * sn[ma.x], dpdy[ma.z]
        );
    uvd1 = float2x2(
            dpdx[me.y] * sn[me.x], dpdx[me.z],
            dpdy[me.y] * sn[me.x], dpdy[me.z]
        );
    t = m.y / (m.x + m.y);
}

void biplanar_texture_float(Texture2DArray tex, SamplerState state, float index, float2 uv, float2x2 uvd, out float4 rgba) {
    rgba = tex.SampleGrad(state, float3(uv.x, uv.y, index),
                                 float3(uvd[0][0], uvd[0][1], index),
                                 float3(uvd[1][0], uvd[1][1], index));
}

void biplanar_float(Texture2DArray tex, float index, SamplerState state, in float3 p, in float3 n, in float k,
                    out float3 rgb, out float3 emission) {
    // grab coord derivatives for texturing
    float3 dpdx = ddx(p);
    float3 dpdy = ddy(p);
    const float3 pn = abs(n);
    const float3 sn = float3(sign(n.x), -sign(n.y), -sign(n.z));

    // Major axis
    const uint3 ma = (pn.x >= pn.y && pn.x >= pn.z) ? uint3(0, 2, 1) :
                     (pn.y >= pn.z)                 ? uint3(1, 2, 0) :
                                                      uint3(2, 0, 1);
    
    
    // Minor axis
    const int3 me = (ma[0] != 0 && (pn.x >= pn.y || pn.x >= pn.z)) ? uint3(0, 2, 1) :
                    (ma[0] != 1 && (pn.y >= pn.z || pn.y > pn.x))  ? uint3(1, 2, 0) :
                                                                     uint3(2, 0, 1);

    // project + fetch
    float4 x = tex.SampleGrad(state, float3(   p[ma.y] * sn[ma.x],    p[ma.z], index),
                                     float3(dpdx[ma.y] * sn[ma.x], dpdx[ma.z], index),
                                     float3(dpdy[ma.y] * sn[ma.x], dpdy[ma.z], index));
    
    float4 y = tex.SampleGrad(state, float3(   p[me.y],    p[me.z], index),
                                     float3(dpdx[me.y], dpdx[me.z], index),
                                     float3(dpdy[me.y], dpdy[me.z], index));
    
    // blend and return
    const float2 dNorm = normalize(float2(pn[ma.x], pn[me.x]));
    float2 m = float2(dNorm.x, dNorm.y);
    m = clamp((m - 0.5773) / (1.0 - 0.5773), 0.0, 1.0);
    m = pow(m * m, float2(k / 8.0, k / 8.0));

    // Output Difuse/Albedo Color
    rgb = float3(
        (x.r * m.x + y.r * m.y) / (m.x + m.y),
        (x.g * m.x + y.g * m.y) / (m.x + m.y),
        (x.b * m.x + y.b * m.y) / (m.x + m.y)
    );

    // Output Emission
    emission = rgb * (1 - (x.a * m.x + y.a * m.y) / (m.x + m.y));
}

float3 normal_strength(float2 normal_in, float strength) {
    float3 normal = float3(normal_in.x, normal_in.y,  sqrt(1.0 - (normal_in.x * normal_in.x) - (normal_in.y * normal_in.y)));
    normal = normal * 2 - 1;
    return float3(normal.rg * strength, lerp(1, normal.b, saturate(strength)));
}

void biplanar_normal_float(Texture2DArray tex, float index, SamplerState state, float3 p, float k, float strength, float3 wnrm,
                           out float3 normal, out float smoothness, out float metallic) {
    float3 dpdx = ddx(p);
    float3 dpdy = ddy(p);
    float3 n = abs(wnrm);

    // Major axis
    const uint3 ma = (n.x > n.y && n.x > n.z) ? uint3(0, 1, 2) :
                     (n.y > n.z)              ? uint3(1, 2, 0) :
                                                uint3(2, 0, 1);

    // Minor axis
    const int3 mi = (n.x < n.y && n.x < n.z) ? int3(0, 1, 2) :
                    (n.y < n.z)              ? int3(1, 2, 0) :
                                               int3(2, 0, 1);

    // Horizontal Axis
    const int3 mc = (n.x > n.y && n.x > n.z) ? int3(0, 2, 1) : ma;
    const int rev = (n.x > n.y && n.x > n.z) ? -1 : 1;

    // Median axis (in x; yz are following axis)
    uint3 me = 3 - mi - ma;

    // Project + fetch
    float4 x = tex.SampleGrad(state, float3(   p[mc.y] * rev,    p[mc.z], index), 
                                     float3(dpdx[mc.y] * rev, dpdx[mc.z], index), 
                                     float3(dpdy[mc.y] * rev, dpdy[mc.z], index));

    float4 y = tex.SampleGrad(state, float3(   p[me.y],    p[me.z], index), 
                                     float3(dpdx[me.y], dpdx[me.z], index),
                                     float3(dpdy[me.y], dpdy[me.z], index));

    // Normal vector extraction
    float3 n1 = normal_strength(x.xy, strength);
    float3 n2 = normal_strength(y.xy, strength);

    // UDN-style normal blending
    n1 = normalize(float3(n1.y + wnrm[ma.z], n1.x + wnrm[ma.y], wnrm[ma.x]));
    n2 = normalize(float3(n2.y + wnrm[me.z], n2.x + wnrm[me.y], wnrm[me.x]));
    n1 = float3(n1[ma.z], n1[ma.y], n1[ma.x]);
    n2 = float3(n2[me.z], n2[me.y], n2[me.x]);

    // Blend factors Normal
    float2 m = float2(n[ma.x], n[me.x]);
    m = clamp((m - 0.5773) / (1.0 - 0.5773), 0.0, 1.0);
    m = pow(m, float2(k / 8.0, k / 8.0));
    
    // Output Normal
    normal = normalize((n1 * m.x + n2 * m.y) / (m.x + m.y));
    
    // Output Smoothness
    smoothness = (x.b * m.x + y.b * m.y) / (m.x + m.y);
    
    // Output Metalic
    metallic = 1 - (x.a * m.x + y.a * m.y) / (m.x + m.y);
}

void interpolate_normals_float(float3 normal1, float3 normal2, float3 normal3, float w1, float w2, float w3, out float3 normal)  {
    const float total_weight = w1 + w2 + w3;
    
    const float t1 = w1 / total_weight;
    const float t2 = w2 / total_weight;
    const float t3 = w3 / total_weight;
    
    float3 result = lerp(normal1, normal2, t1 + t2);
    result = lerp(result, normal3, t3);
    
    normal = total_weight < 0.0001 ? normal1 : normalize(result);
}

void unpack_palette_float(in float4 uv0, out float3 uv1) {
    uv1 = uv0.w <= 1.1 ? float3(1, 0, 0) : uv0.w <= 2.1 ? float3(0, 1, 0) : float3(0, 0, 1);
}

void biplanar_cubes_float(
    Texture2DArray tex, Texture2DArray map, SamplerState state, float3 p, float3 n, float3 palette, float3 material,
    out float4 rgba, out float3 normal, out float metallic, out float smoothness, out float4 emission, out float aocclusion) {
    
    float3 dpdx = ddx(p);
    float3 dpdy = ddy(p);
    const uint3 value[] = {
        uint3(0, 2, 1),
        uint3(1, 2, 0),
        uint3(2, 0, 1)
    };
    const float3 mat = round(abs(palette)) - 1;
    const float3 pn = abs(n);
    const float3 sn = float3(sign(n.x), -sign(n.y), -sign(n.z));

    const uint largest = (pn.x >= pn.y && pn.x >= pn.z) ? 0 : (pn.y >= pn.z) ? 1 : 2;
    const uint smallest = (pn.z <= pn.y && pn.z <= pn.x) ? 2 : (pn.y <= pn.x) ? 1 : 0;
    const uint middle = (3 - largest - smallest);
    
    const uint3 ma = value[largest];
    const uint3 mi = value[smallest];
    const uint3 me = value[middle];
    
    const float z = pow(pn[mi.x] / 0.58, 8) * 4;
    float2 m = normalize(float2(pn[ma.x], pn[me.x]));
    m = clamp((m - 0.5773) / (1.0 - 0.5773), 0.0, 1.0);
    m = pow(m * m, z + 2);
    
    const float t = m.y / (m.x + m.y);

    float2 uv0 =  float2(p[ma.y] * sn[ma.x], p[ma.z]);
    float2 uv1 =  float2(p[me.y] * sn[me.x], p[me.z]);
    
    float2 dx0 =  float2(dpdx[ma.y] * sn[ma.x], dpdx[ma.z]);
    float2 dy0 =  float2(dpdy[ma.y] * sn[ma.x], dpdy[ma.z]);
    float2 dx1 =  float2(dpdx[me.y] * sn[me.x], dpdx[me.z]);
    float2 dy1 =  float2(dpdy[me.y] * sn[me.x], dpdy[me.z]);
    
    float4 col = float4(0, 0, 0, 0);
    for (int i = 0; i < 3; ++i) {
        float4 ca = tex.SampleGrad(state,
            float3(uv0.x, uv0.y, mat[i]),
            float3(dx0.x, dx0.y, mat[i]),
            float3(dy0.x, dy0.y, mat[i]));
        float4 cb = tex.SampleGrad(state,
            float3(uv1.x, uv1.y, mat[i]),
            float3(dx1.x, dx1.y, mat[i]),
            float3(dy1.x, dy1.y, mat[i]));
        col += lerp(ca, cb, t) * material[i];
    }
    
    float3 nor = float3(0, 0, 0);
    float2 ext = float2(0, 0);
    for (int i = 0; i < 3; ++i) {
        float4 ca = map.SampleGrad(state,
            float3(uv0.x, uv0.y, mat[i]),
            float3(dx0.x, dx0.y, mat[i]),
            float3(dy0.x, dy0.y, mat[i]));
        float4 cb = map.SampleGrad(state,
            float3(uv1.x, uv1.y, mat[i]),
            float3(dx1.x, dx1.y, mat[i]),
            float3(dy1.x, dy1.y, mat[i]));
        ca.xy = ca.xy * 2 - 1;
        cb.xy = cb.xy * 2 - 1;
        nor += lerp(float3(ca.xy, sqrt(abs(1.0 - ca.x * ca.x - ca.y * ca.y))),
                    float3(cb.xy, sqrt(abs(1.0 - cb.x * cb.x - cb.y * cb.y))), t) * material[i];
        ext += lerp(ca.zw, cb.zw, t) * material[i];
    }
    nor = normalize(nor);

    rgba = float4(col.rgb, 1);
    emission = float4(clamp((col.a - 0.5) * 4.0, 0.0, 1.0) * col.rgb, 1);
    aocclusion = clamp(col.a * 2.0, 0.0, 1.0);
    normal = nor;
    smoothness = ext.x;
    metallic = 1 - ext.y;
}

#endif // BIPLANAR_INCLUDE