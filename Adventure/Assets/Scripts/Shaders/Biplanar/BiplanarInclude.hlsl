#ifndef BIPLANAR_INCLUDE
#define BIPLANAR_INCLUDE

void biplanar_float(Texture2D tex, SamplerState state, in float3 p, in float3 n, in float k,
                    out float4 rgba, out float smooth)
{
    // grab coord derivatives for texturing
    float3 dpdx = ddx(p);
    float3 dpdy = ddy(p);
    n = abs(n);

    // major axis (in x; yz are following axis)
    int3 ma = (n.x > n.y && n.x > n.z) ? int3(0, 1, 2) :
              (n.y > n.z)              ? int3(1, 2, 0) :
                                         int3(2, 0, 1);

    // minor axis (in x; yz are following axis)
    int3 mi = (n.x < n.y && n.x < n.z) ? int3(0, 1, 2) :
              (n.y < n.z)              ? int3(1, 2, 0) :
                                         int3(2, 0, 1);

    // median axis (in x;  yz are following axis)
    int3 me = int3(3, 3, 3) - mi - ma;

    // project+fetch
    float4 x = tex.SampleGrad(state, float2(p[ma.y], p[ma.z]),
                                     float2(dpdx[ma.y], dpdx[ma.z]),
                                     float2(dpdy[ma.y], dpdy[ma.z]));
    
    float4 y = tex.SampleGrad(state, float2(p[me.y], p[me.z]),
                                     float2(dpdx[me.y], dpdx[me.z]),
                                     float2(dpdy[me.y], dpdy[me.z]));
    
    // blend and return
    float2 m = float2(n[ma.x], n[me.x]);
    // optional - add local support (prevents discontinuity)
    m = clamp((m - 0.5773) / (1.0 - 0.5773), 0.0, 1.0);
    // transition control
    m = pow(m, float2(k / 8.0, k / 8.0));

    // Output Difuse/Albedo Color
    rgba = float4(
        (x.r * m.x + y.r * m.y) / (m.x + m.y),
        (x.g * m.x + y.g * m.y) / (m.x + m.y),
        (x.b * m.x + y.b * m.y) / (m.x + m.y),
        1
    );

    // Output Smoothness Extra Data
    smooth = (x.a * m.x + y.a * m.y) / (m.x + m.y);
}

void biplanar_normal_float(Texture2D tex, SamplerState state, float3 wpos, float3 wtan, float3 wbtn, float3 wnrm, in float k,
                           out float3 normal, out float metal) {
    // Coordinate derivatives for texturing
    float3 p = wpos;
    float3 n = abs(wnrm);
    float3 dpdx = ddx(p);
    float3 dpdy = ddy(p);

    // Major axis (in x; yz are following axis)
    uint3 ma = (n.x > n.y && n.x > n.z) ? uint3(0, 1, 2) :
               (n.y > n.z             ) ? uint3(1, 2, 0) :
                                          uint3(2, 0, 1) ;

    // Minor axis (in x; yz are following axis)
    uint3 mi = (n.x < n.y && n.x < n.z) ? uint3(0, 1, 2) :
               (n.y < n.z             ) ? uint3(1, 2, 0) :
                                          uint3(2, 0, 1) ;

    // Median axis (in x; yz are following axis)
    uint3 me = 3 - mi - ma;

    // Project + fetch
    float4 x = tex.SampleGrad(state, float2(   p[ma.y],    p[ma.z]), 
                                     float2(dpdx[ma.y], dpdx[ma.z]), 
                                     float2(dpdy[ma.y], dpdy[ma.z]));

    float4 y = tex.SampleGrad(state, float2(   p[me.y],    p[me.z]), 
                                     float2(dpdx[me.y], dpdx[me.z]),
                                     float2(dpdy[me.y], dpdy[me.z]));

    // Normal vector extraction
    float3 n1 = UnpackNormalmapRGorAG(x);
    float3 n2 = UnpackNormalmapRGorAG(y);

    // Do UDN-style normal blending in the tangent space then bring the result
    // back to the world space. To make the space conversion simpler, we use
    // reverse-order swizzling, which brings us back to the original space by
    // applying twice.
    n1 = normalize(float3(n1.y + wnrm[ma.z], n1.x + wnrm[ma.y], wnrm[ma.x]));
    n2 = normalize(float3(n2.y + wnrm[me.z], n2.x + wnrm[me.y], wnrm[me.x]));
    n1 = float3(n1[ma.z], n1[ma.y], n1[ma.x]);
    n2 = float3(n2[me.z], n2[me.y], n2[me.x]);

    // Blend factors Normal
    float2 w = float2(n[ma.x], n[me.x]);
    w = saturate((w - 0.5773) / (1 - 0.5773));
    normal = normalize((n1 * w.x + n2 * w.y) / (w.x + w.y));
    
    // Blend factors Color
    float2 m = float2(n[ma.x], n[me.x]);
    m = clamp((m - 0.5773) / (1.0 - 0.5773), 0.0, 1.0);
    m = pow(m, float2(k / 8.0, k / 8.0));
    
    // Output Normal
    normal = TransformWorldToTangent(normal, float3x3(wtan, wbtn, wnrm));
    
    // Output Metallic Extra Data
    metal = (x.a * m.x + y.a * m.y) / (m.x + m.y);
}
#endif // BIPLANAR_INCLUDE