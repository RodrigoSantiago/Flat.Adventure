// ------------------------------------------------------------
// Cheap 3D Perlin Noise
// ------------------------------------------------------------

float Hash3D(float3 p) {
    p = frac(p * 0.1031);
    p += dot(p, p.yzx + 33.33);

    return frac((p.x + p.y) * p.z);
}

float CheapNoise3D(float3 p) {
    float3 i = floor(p);
    float3 f = frac(p);

    // Smooth interpolation
    f = f * f * (3.0 - 2.0 * f);

    float n000 = Hash3D(i + float3(0,0,0));
    float n100 = Hash3D(i + float3(1,0,0));
    float n010 = Hash3D(i + float3(0,1,0));
    float n110 = Hash3D(i + float3(1,1,0));

    float n001 = Hash3D(i + float3(0,0,1));
    float n101 = Hash3D(i + float3(1,0,1));
    float n011 = Hash3D(i + float3(0,1,1));
    float n111 = Hash3D(i + float3(1,1,1));

    float x00 = lerp(n000, n100, f.x);
    float x10 = lerp(n010, n110, f.x);
    float x01 = lerp(n001, n101, f.x);
    float x11 = lerp(n011, n111, f.x);

    float y0 = lerp(x00, x10, f.y);
    float y1 = lerp(x01, x11, f.y);

    return lerp(y0, y1, f.z);
}

// ------------------------------------------------------------
// Color function
// ------------------------------------------------------------

void TriplanarNoiseColor_float(
    float3 globalPosition,
    float4 colorA,
    float4 colorB,
    float scale,
    out float4 col
)
{
    float noiseA = CheapNoise3D((globalPosition + float3(100, 25, 336)) / scale);
    float noiseB = CheapNoise3D((globalPosition + float3(500, 600, 210)) / scale * 4);

    float noise = noiseA * 0.8 + noiseB * 0.2;
    
    col = lerp(colorA, colorB, noise);
}