float4 SampleTexture(UnityTexture2DArray unityTexture, const int matId, const float2 uv, const float2 dx, const float2 dy) {
    return SAMPLE_TEXTURE2D_ARRAY_GRAD(unityTexture.tex, unityTexture.samplerstate, uv, matId, dx, dy);
}

float3 UnpackNormal(const float2 xy){
    float3 normal;
    normal.xy = xy * 2.0 - 1.0;
    normal.z = sqrt(1.0 - saturate(dot(normal.xy, normal.xy)));
    return normal;
}

void Biplanar_float(
    UnityTexture2DArray Albedo,
    UnityTexture2DArray Normal,
    float3 Position,
    float3 NormalWS,
    float Scale,
    float4 PackUv0,
    float4 PackUv1,
    bool multiTexture,
    
    out float3 OutAlbedo,
    out float3 OutNormal,
    out float OutRoughness,
    out float OutMetallic,
    out float OutEmission
    )
{
    
    // ============================================================
    //          Bi-planar Projection
    // ============================================================
    float3 n = normalize(NormalWS);
    float3 absN = abs(n);

    float3 weights = pow(absN, 4.0);
    float minWeight = min(weights.x, min(weights.y, weights.z));
    float3 biWeights = max(weights - minWeight, 0.001);

    int3 ma = absN.x >= absN.y && absN.x >= absN.z ? int3(0,1,2) : (absN.y >= absN.z ? int3(1,2,0) : int3(2,0,1));
    if (absN[ma.y] < absN[ma.z])  {
        int temp = ma.y;
        ma.y = ma.z;
        ma.z = temp;
    }

    float2 w = float2(biWeights[ma.x], biWeights[ma.y]);
    w /= (w.x + w.y);

    // ============================================================
    //          Pre-Compute Sampling
    // ============================================================
    
    float2 uvX = Position.zy * Scale;
    float2 uvY = Position.xz * Scale;
    float2 uvZ = Position.xy * Scale;
    if (n.x < 0) uvX.x = -uvX.x;
    if (n.y < 0) uvY.x = -uvY.x;
    if (n.z < 0) uvZ.x = -uvZ.x;

    const float2 dxX = ddx(uvX), dyX = ddy(uvX);
    const float2 dxY = ddx(uvY), dyY = ddy(uvY);
    const float2 dxZ = ddx(uvZ), dyZ = ddy(uvZ);

    float2 uv1, uv2;
    float2 dx1, dy1, dx2, dy2;

    if (ma.x == 0) { uv1 = uvX; dx1 = dxX; dy1 = dyX; }
    else if (ma.x == 1) { uv1 = uvY; dx1 = dxY; dy1 = dyY; }
    else { uv1 = uvZ; dx1 = dxZ; dy1 = dyZ; }

    if (ma.y == 0) { uv2 = uvX; dx2 = dxX; dy2 = dyX; }
    else if (ma.y == 1) { uv2 = uvY; dx2 = dxY; dy2 = dyY; }
    else { uv2 = uvZ; dx2 = dxZ; dy2 = dyZ; }
    
    const int mat0 = (int)round(PackUv0[0]);
    const int mat1 = (int)round(PackUv0[1]);
    const int mat2 = (int)round(PackUv0[2]);
    
    // ============================================================
    //          Color[RGB] + Emission[A]
    // ============================================================
    
    float4 colorA0 = SampleTexture(Albedo, mat0, uv1, dx1, dy1);
    float4 colorB0 = SampleTexture(Albedo, mat0, uv2, dx2, dy2);
    if (multiTexture) {
        const float4 colorA1 = SampleTexture(Albedo, mat1, uv1, dx1, dy1);
        const float4 colorB1 = SampleTexture(Albedo, mat1, uv2, dx2, dy2);
        
        const float4 colorA2 = SampleTexture(Albedo, mat2, uv1, dx1, dy1);
        const float4 colorB2 = SampleTexture(Albedo, mat2, uv2, dx2, dy2);
        
        colorA0 = PackUv1.x * colorA0 + PackUv1.y * colorA1 + PackUv1.z * colorA2;
        colorB0 = PackUv1.x * colorB0 + PackUv1.y * colorB1 + PackUv1.z * colorB2;
    }
    OutAlbedo = colorA0.xyz * w.x + colorB0.xyz * w.y;
    OutEmission = colorA0.w * w.x + colorB0.w * w.y;

    // ============================================================
    //          Normals[RG] + Roughness[B] + Metallic[A]
    // ============================================================
    
    float4 extraA0 = SampleTexture(Normal, mat0, uv1, dx1, dy1);
    float4 extraB0 = SampleTexture(Normal, mat0, uv2, dx2, dy2);
    float3 normalA0 = UnpackNormal(extraA0.rg);
    float3 normalB0 = UnpackNormal(extraB0.rg);
    if (multiTexture) {
        const float4 extraA1 = SampleTexture(Normal, mat1, uv1, dx1, dy1);
        const float4 extraB1 = SampleTexture(Normal, mat1, uv2, dx2, dy2);
        const float3 normalA1 = UnpackNormal(extraA1.rg);
        const float3 normalB1 = UnpackNormal(extraB1.rg);
        
        const float4 extraA2 = SampleTexture(Normal, mat2, uv1, dx1, dy1);
        const float4 extraB2 = SampleTexture(Normal, mat2, uv2, dx2, dy2);
        const float3 normalA2 = UnpackNormal(extraA2.rg);
        const float3 normalB2 = UnpackNormal(extraB2.rg);
        
        extraA0 = PackUv1.x * extraA0 + PackUv1.y * extraA1 + PackUv1.z * extraA2;
        extraB0 = PackUv1.x * extraB0 + PackUv1.y * extraB1 + PackUv1.z * extraB2;
        
        normalA0 = normalize(PackUv1.x * normalA0 + PackUv1.y * normalA1 + PackUv1.z * normalA2);
        normalB0 = normalize(PackUv1.x * normalB0 + PackUv1.y * normalB1 + PackUv1.z * normalB2);
    }
    OutRoughness = extraA0.z * w.x + extraB0.z * w.y;
    OutMetallic = extraA0.w * w.x + extraB0.w * w.y;

    // ============================================================
    //          Tangent Space to World Space
    // ============================================================
    
    const float3 T[3] = { float3(0, 0, 1), float3(1, 0, 0), float3(1, 0, 0) };
    const float3 B[3] = { float3(0, 1, 0), float3(0, 0, 1), float3(0, 1, 0) };
    const float3 Q[3] = { float3(1, 0, 0), float3(0, 1, 0), float3(0, 0, 1) };

    float3 t1 = T[ma.x] * sign(n[ma.x]), b1 = B[ma.x], q1 = Q[ma.x] * sign(n[ma.x]);
    float3 t2 = T[ma.y] * sign(n[ma.y]), b2 = B[ma.y], q2 = Q[ma.y] * sign(n[ma.y]);

    float3 n1 = normalize(t1 * normalA0.x + b1 * normalA0.y + q1 * normalA0.z);
    float3 n2 = normalize(t2 * normalB0.x + b2 * normalB0.y + q2 * normalB0.z);

    OutNormal = normalize(n1 * w.x + n2 * w.y);
}