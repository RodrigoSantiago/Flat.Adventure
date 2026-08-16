static const float3 MatColors[4] =
{
    float3(1.0, 0.0, 0.0), // 0 - Vermelho
    float3(0.0, 1.0, 0.0), // 1 - Verde
    float3(0.0, 0.0, 1.0), // 2 - Azul
    float3(1.0, 1.0, 0.0)  // 3 - Amarelo
};

void Biplanar_float(
    UnityTexture2D Albedo,
    UnityTexture2D Normal,
    float3 Position,
    float3 NormalWS,
    float Scale,
    float4 MatIndex,
    float4 TriangleIndex,
    
    out float3 OutAlbedo,
    out float3 OutNormal
    )
{
    float3 n = normalize(NormalWS);
    float3 absN = abs(n);

    // 1. Pesos contínuos
    float3 weights = pow(absN, 4.0);
    float minWeight = min(weights.x, min(weights.y, weights.z));
    float3 biWeights = max(weights - minWeight, 0.001);

    // Seleção de eixos
    int3 ma = absN.x >= absN.y && absN.x >= absN.z ? int3(0,1,2) : (absN.y >= absN.z ? int3(1,2,0) : int3(2,0,1));
    if (absN[ma.y] < absN[ma.z]) 
    {
        int temp = ma.y;
        ma.y = ma.z;
        ma.z = temp;
    }

    float2 w = float2(biWeights[ma.x], biWeights[ma.y]);
    w /= (w.x + w.y);

    // 2. Cálculo dos 3 pares de UV e seus gradientes de tela (Derivadas explícitas)
    float2 uvX = Position.zy * Scale;
    float2 uvY = Position.xz * Scale;
    float2 uvZ = Position.xy * Scale;

    // Gradientes que impedem a GPU de confundir as bordas com saltos de mipmap
    float2 dxX = ddx(uvX), dyX = ddy(uvX);
    float2 dxY = ddx(uvY), dyY = ddy(uvY);
    float2 dxZ = ddx(uvZ), dyZ = ddy(uvZ);

    // Seleção manual de UVs e Derivadas para cada eixo escolhido
    float2 uv1, uv2;
    float2 dx1, dy1, dx2, dy2;

    if (ma.x == 0) { uv1 = uvX; dx1 = dxX; dy1 = dyX; }
    else if (ma.x == 1) { uv1 = uvY; dx1 = dxY; dy1 = dyY; }
    else { uv1 = uvZ; dx1 = dxZ; dy1 = dyZ; }

    if (ma.y == 0) { uv2 = uvX; dx2 = dxX; dy2 = dyX; }
    else if (ma.y == 1) { uv2 = uvY; dx2 = dxY; dy2 = dyY; }
    else { uv2 = uvZ; dx2 = dxZ; dy2 = dyZ; }

    // 3. Amostragem usando SAMPLE_TEXTURE2D_GRAD (Fix da linha de 1px)
    float3 col1 = SAMPLE_TEXTURE2D_GRAD(Albedo.tex, Albedo.samplerstate, uv1, dx1, dy1).rgb;
    float3 col2 = SAMPLE_TEXTURE2D_GRAD(Albedo.tex, Albedo.samplerstate, uv2, dx2, dy2).rgb;
    OutAlbedo = col1 * w.x + col2 * w.y;

    // 4. Amostragem do Normal Map com Gradientes
    float3 p1 = UnpackNormal(SAMPLE_TEXTURE2D_GRAD(Normal.tex, Normal.samplerstate, uv1, dx1, dy1));
    float3 p2 = UnpackNormal(SAMPLE_TEXTURE2D_GRAD(Normal.tex, Normal.samplerstate, uv2, dx2, dy2));

    // Reconstrução de Espaço Tangente
    float3 T[3] = { float3(0,0,1), float3(1,0,0), float3(1,0,0) };
    float3 B[3] = { float3(0,1,0), float3(0,0,1), float3(0,1,0) };
    float3 Q[3] = { float3(1,0,0), float3(0,1,0), float3(0,0,1) };

    float3 t1 = T[ma.x] * sign(n[ma.x]), b1 = B[ma.x], q1 = Q[ma.x] * sign(n[ma.x]);
    float3 t2 = T[ma.y] * sign(n[ma.y]), b2 = B[ma.y], q2 = Q[ma.y] * sign(n[ma.y]);

    float3 n1 = normalize(t1 * p1.x + b1 * p1.y + q1 * p1.z);
    float3 n2 = normalize(t2 * p2.x + b2 * p2.y + q2 * p2.z);

    OutNormal = normalize(n1 * w.x + n2 * w.y);
    

    // ============================================================
    // 7. MATERIAL — feito SOMENTE no final
    // ============================================================

    float3 color0 = MatColors[(int)round(MatIndex.x)];
    float3 color1 = MatColors[(int)round(MatIndex.y)];
    float3 color2 = MatColors[(int)round(MatIndex.z)];

    float3 materialColor =
        color0 * TriangleIndex.x +
        color1 * TriangleIndex.y +
        color2 * TriangleIndex.z;

    OutAlbedo *= materialColor;
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