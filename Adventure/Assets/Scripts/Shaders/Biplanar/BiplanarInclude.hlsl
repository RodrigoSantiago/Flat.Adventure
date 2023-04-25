#ifndef BIPLANAR_INCLUDE
#define BIPLANAR_INCLUDE

void biplanar_float(Texture2DArray tex, float index, SamplerState state, in float3 p, in float3 n, in float k,
                    out float3 rgb, out float3 emission) {
    // grab coord derivatives for texturing
    float3 dpdx = ddx(p);
    float3 dpdy = ddy(p);
    n = abs(n);

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

    // Median axis
    int3 me = int3(3, 3, 3) - mi - ma;

    // project + fetch
    float4 x = tex.SampleGrad(state, float3(   p[mc.y] * rev,    p[mc.z], index),
                                     float3(dpdx[mc.y] * rev, dpdx[mc.z], index),
                                     float3(dpdy[mc.y] * rev, dpdy[mc.z], index));
    
    float4 y = tex.SampleGrad(state, float3(   p[me.y],    p[me.z], index),
                                     float3(dpdx[me.y], dpdx[me.z], index),
                                     float3(dpdy[me.y], dpdy[me.z], index));
    
    // blend and return
    float2 m = float2(n[ma.x], n[me.x]);
    m = clamp((m - 0.5773) / (1.0 - 0.5773), 0.0, 1.0);
    m = pow(m, float2(k / 8.0, k / 8.0));

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

#endif // BIPLANAR_INCLUDE