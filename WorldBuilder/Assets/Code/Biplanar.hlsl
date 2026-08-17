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
    float4 PackUv2,
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
    if (n.x < 0)
        uvX.x = -uvX.x;

    if (n.y < 0)
        uvY.x = -uvY.x;

    if (n.z < 0)
        uvZ.x = -uvZ.x;

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
    const int mat0 = (int)round(abs(PackUv1.x - PackUv1.y) < 0.01 ? min(PackUv0.x, PackUv0.y) : (PackUv1.x > PackUv1.y ? PackUv0.x : PackUv0.y));
    const int mat1 = (int)round(abs(PackUv1.z - PackUv1.w) < 0.01 ? min(PackUv0.z, PackUv0.w) : (PackUv1.z > PackUv1.w ? PackUv0.z : PackUv0.w));
    const int mat2 = (int)round(abs(PackUv2.z - PackUv2.w) < 0.01 ? min(PackUv2.x, PackUv2.y) : (PackUv2.z > PackUv2.w ? PackUv2.x : PackUv2.y));
    
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
        
        colorA0 = (PackUv1.x + PackUv1.y) * colorA0 +
                  (PackUv1.z + PackUv1.w) * colorA1 +
                  (PackUv2.z + PackUv2.w) * colorA2;
        
        colorB0 = (PackUv1.x + PackUv1.y) * colorB0 +
                  (PackUv1.z + PackUv1.w) * colorB1 +
                  (PackUv2.z + PackUv2.w) * colorB2;
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
        
        extraA0 = (PackUv1.x + PackUv1.y) * extraA0 +
                  (PackUv1.z + PackUv1.w) * extraA1 +
                  (PackUv2.z + PackUv2.w) * extraA2;
        
        extraB0 = (PackUv1.x + PackUv1.y) * extraB0 +
                  (PackUv1.z + PackUv1.w) * extraB1 +
                  (PackUv2.z + PackUv2.w) * extraB2;
        
        normalA0 = normalize(
                    (PackUv1.x + PackUv1.y) * normalA0 +
                    (PackUv1.z + PackUv1.w) * normalA1 +
                    (PackUv2.z + PackUv2.w) * normalA2
                   );
        
        normalB0 = normalize(
                    (PackUv1.x + PackUv1.y) * normalB0 +
                    (PackUv1.z + PackUv1.w) * normalB1 +
                    (PackUv2.z + PackUv2.w) * normalB2
                   );
    }
    OutRoughness = extraA0.z * w.x + extraB0.z * w.y;
    OutMetallic = extraA0.w * w.x + extraB0.w * w.y;

    // Tangent Space
    const float3 T[3] = { float3(0, 0, 1), float3(1, 0, 0), float3(1, 0, 0) };
    const float3 B[3] = { float3(0, 1, 0), float3(0, 0, 1), float3(0, 1, 0) };
    const float3 Q[3] = { float3(1, 0, 0), float3(0, 1, 0), float3(0, 0, 1) };

    float3 t1 = T[ma.x] * sign(n[ma.x]), b1 = B[ma.x], q1 = Q[ma.x] * sign(n[ma.x]);
    float3 t2 = T[ma.y] * sign(n[ma.y]), b2 = B[ma.y], q2 = Q[ma.y] * sign(n[ma.y]);

    float3 n1 = normalize(t1 * normalA0.x + b1 * normalA0.y + q1 * normalA0.z);
    float3 n2 = normalize(t2 * normalB0.x + b2 * normalB0.y + q2 * normalB0.z);

    OutNormal = normalize(n1 * w.x + n2 * w.y);
}
/*void Triplanar_float(UnityTexture2D Albedo,UnityTexture2D Normal,float3 Position,float3 NormalWS,float Scale,out float3 OutAlbedo,out float3 OutNormal)
{
    float3 n=normalize(NormalWS);
    float3 w=pow(abs(n),4.0);
    w/=max(w.x+w.y+w.z,1e-5);

    float2 uvX=Position.zy*Scale;
    float2 uvY=Position.xz*Scale;
    float2 uvZ=Position.xy*Scale;

    OutAlbedo=
        SAMPLE_TEXTURE2D(Albedo.tex,Albedo.samplerstate,uvX).rgb*w.x+
        SAMPLE_TEXTURE2D(Albedo.tex,Albedo.samplerstate,uvY).rgb*w.y+
        SAMPLE_TEXTURE2D(Albedo.tex,Albedo.samplerstate,uvZ).rgb*w.z;

    float3 pX=UnpackNormal(SAMPLE_TEXTURE2D(Normal.tex,Normal.samplerstate,uvX));
    float3 pY=UnpackNormal(SAMPLE_TEXTURE2D(Normal.tex,Normal.samplerstate,uvY));
    float3 pZ=UnpackNormal(SAMPLE_TEXTURE2D(Normal.tex,Normal.samplerstate,uvZ));

    float3 tX=float3(0,0,1),bX=float3(0,1,0),qX=float3(1,0,0);
    float3 tY=float3(1,0,0),bY=float3(0,0,1),qY=float3(0,1,0);
    float3 tZ=float3(1,0,0),bZ=float3(0,1,0),qZ=float3(0,0,1);

    tX*=sign(n.x); qX*=sign(n.x);
    tY*=sign(n.y); qY*=sign(n.y);
    tZ*=sign(n.z); qZ*=sign(n.z);

    float3 nx=normalize(tX*pX.x+bX*pX.y+qX*pX.z);
    float3 ny=normalize(tY*pY.x+bY*pY.y+qY*pY.z);
    float3 nz=normalize(tZ*pZ.x+bZ*pZ.y+qZ*pZ.z);

    OutNormal=normalize(nx*w.x+ny*w.y+nz*w.z);
}*/