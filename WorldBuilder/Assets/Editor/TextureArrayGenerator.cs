using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class TextureArrayGenerator : EditorWindow
{
    private DefaultAsset inputFolder;
    private int selectedResolutionIndex = 1;

    private readonly string[] resolutionLabels = { "4096 (4K)", "2048 (2K)", "1024 (1K)", "512", "256" };
    private readonly int[] resolutionValues = { 4096, 2048, 1024, 512, 256 };

    private class TextureGroup
    {
        public string BaseName;
        public string FullPath;
        public Texture2D Albedo;
        public Texture2D Emission;
        public Texture2D Normal;
        public Texture2D Roughness;
        public Texture2D Metallic;
    }

    [MenuItem("Tools/Texture Array Builder")]
    public static void ShowWindow()
    {
        GetWindow<TextureArrayGenerator>("Texture Array Builder");
    }

    private void OnGUI()
    {
        GUILayout.Label("Configuração de Texture2DArray", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        inputFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "Pasta de Origem",
            inputFolder,
            typeof(DefaultAsset),
            false
        );

        selectedResolutionIndex = EditorGUILayout.Popup(
            "Resolução (Pixels)",
            selectedResolutionIndex,
            resolutionLabels
        );

        EditorGUILayout.Space();

        if (GUILayout.Button("Processar e Gerar Arrays", GUILayout.Height(35)))
        {
            if (inputFolder == null)
            {
                EditorUtility.DisplayDialog("Erro", "Por favor, selecione uma pasta válida.", "OK");
                return;
            }

            string folderPath = AssetDatabase.GetAssetPath(inputFolder);

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                EditorUtility.DisplayDialog("Erro", "O objeto selecionado não é uma pasta.", "OK");
                return;
            }

            ProcessFolder(folderPath, resolutionValues[selectedResolutionIndex]);
        }
    }

    private void ProcessFolder(string folderPath, int targetSize)
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });

        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Aviso",
                "Nenhuma textura encontrada na pasta informada.",
                "OK"
            );
            return;
        }

        Dictionary<string, TextureGroup> groups = new Dictionary<string, TextureGroup>(
            StringComparer.OrdinalIgnoreCase
        );

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            if (tex == null)
                continue;

            string fileName = Path.GetFileNameWithoutExtension(path);
            ParseAndGroupTexture(path, fileName, tex, groups);
        }

        if (groups.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Aviso",
                "Nenhum grupo de textura válido foi identificado.",
                "OK"
            );
            return;
        }

        List<TextureGroup> sortedGroups = groups.Values
            .OrderBy(g => g.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        int sliceCount = sortedGroups.Count;

        Texture2DArray colorArray = new Texture2DArray(
            targetSize,
            targetSize,
            sliceCount,
            TextureFormat.RGBA32,
            true,
            false
        )
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Repeat
        };

        Texture2DArray normalRMArray = new Texture2DArray(
            targetSize,
            targetSize,
            sliceCount,
            TextureFormat.RGBA32,
            true,
            true
        )
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Repeat
        };

        try
        {
            for (int i = 0; i < sliceCount; i++)
            {
                TextureGroup group = sortedGroups[i];

                EditorUtility.DisplayProgressBar(
                    "Processando Texturas",
                    $"Gerando fatia {i + 1}/{sliceCount}: {group.FullPath}",
                    (float)i / sliceCount
                );

                Color[] albedoPixels = ReadColorPixels(
                    group.Albedo,
                    targetSize,
                    Color.white
                );

                Color[] emissionPixels = ReadColorPixels(
                    group.Emission,
                    targetSize,
                    Color.white
                );

                Color[] normalPixels = ReadNormalPixels(
                    group.Normal,
                    targetSize
                );

                Color[] roughnessPixels = ReadColorPixels(
                    group.Roughness,
                    targetSize,
                    new Color(0.5f, 0.5f, 0.5f, 0.5f)
                );

                Color[] metallicPixels = ReadColorPixels(
                    group.Metallic,
                    targetSize,
                    new Color(0.5f, 0.5f, 0.5f, 0.5f)
                );

                Color[] colorSlice = new Color[targetSize * targetSize];
                Color[] normalRMSlice = new Color[targetSize * targetSize];

                for (int p = 0; p < colorSlice.Length; p++)
                {
                    colorSlice[p] = new Color(
                        albedoPixels[p].r,
                        albedoPixels[p].g,
                        albedoPixels[p].b,
                        emissionPixels[p].r
                    );

                    normalRMSlice[p] = new Color(
                        normalPixels[p].r,
                        normalPixels[p].g,
                        roughnessPixels[p].r,
                        metallicPixels[p].r
                    );
                }

                colorArray.SetPixels(colorSlice, i);
                normalRMArray.SetPixels(normalRMSlice, i);
            }

            colorArray.Apply(true);
            normalRMArray.Apply(true);

            SaveOrReplaceAsset(
                colorArray,
                Path.Combine(folderPath, "Color_Emission_Array.asset")
            );

            SaveOrReplaceAsset(
                normalRMArray,
                Path.Combine(folderPath, "Normal_RM_Array.asset")
            );

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Sucesso!",
                $"Arrays de textura gerados com sucesso na pasta:\n{folderPath}",
                "OK"
            );
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private void ParseAndGroupTexture(
        string path,
        string fileName,
        Texture2D tex,
        Dictionary<string, TextureGroup> groups
    )
    {
        string[] suffixes =
        {
            "_Albedo",
            "_Emission",
            "_Normal",
            "_Roughness",
            "_Metallic"
        };

        string matchedSuffix = suffixes.FirstOrDefault(
            s => fileName.EndsWith(s, StringComparison.OrdinalIgnoreCase)
        );

        if (string.IsNullOrEmpty(matchedSuffix))
            return;

        string baseName = fileName.Substring(
            0,
            fileName.Length - matchedSuffix.Length
        );

        string normalizedPath = path.Replace('\\', '/');
        string groupKey = normalizedPath.Substring(
            0,
            normalizedPath.Length - matchedSuffix.Length - Path.GetExtension(path).Length
        );

        if (!groups.TryGetValue(groupKey, out TextureGroup group))
        {
            group = new TextureGroup
            {
                BaseName = baseName,
                FullPath = groupKey
            };

            groups[groupKey] = group;
        }

        if (matchedSuffix.Equals("_Albedo", StringComparison.OrdinalIgnoreCase))
            group.Albedo = tex;
        else if (matchedSuffix.Equals("_Emission", StringComparison.OrdinalIgnoreCase))
            group.Emission = tex;
        else if (matchedSuffix.Equals("_Normal", StringComparison.OrdinalIgnoreCase))
            group.Normal = tex;
        else if (matchedSuffix.Equals("_Roughness", StringComparison.OrdinalIgnoreCase))
            group.Roughness = tex;
        else if (matchedSuffix.Equals("_Metallic", StringComparison.OrdinalIgnoreCase))
            group.Metallic = tex;
    }

    private Color[] ReadColorPixels(
        Texture2D source,
        int targetSize,
        Color defaultColor
    )
    {
        if (source == null)
        {
            Color[] defaultPixels = new Color[targetSize * targetSize];

            for (int i = 0; i < defaultPixels.Length; i++)
                defaultPixels[i] = defaultColor;

            return defaultPixels;
        }

        RenderTexture rt = RenderTexture.GetTemporary(
            targetSize,
            targetSize,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB
        );

        rt.filterMode = FilterMode.Bilinear;

        Graphics.Blit(source, rt);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D readableTex = new Texture2D(
            targetSize,
            targetSize,
            TextureFormat.RGBA32,
            false,
            true
        );

        readableTex.ReadPixels(
            new Rect(0, 0, targetSize, targetSize),
            0,
            0
        );

        readableTex.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        Color[] pixels = readableTex.GetPixels();

        DestroyImmediate(readableTex);

        return pixels;
    }

    private Color[] ReadNormalPixels(Texture2D source, int targetSize)
    {
        if (source == null)
        {
            Color[] defaultPixels = new Color[targetSize * targetSize];
            Color flatNormal = new Color(0.5f, 0.5f, 1.0f, 1.0f);

            for (int i = 0; i < defaultPixels.Length; i++)
                defaultPixels[i] = flatNormal;

            return defaultPixels;
        }

        RenderTexture rt = RenderTexture.GetTemporary(
            targetSize,
            targetSize,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Linear
        );

        rt.filterMode = FilterMode.Bilinear;

        Material unpackMaterial = GetNormalUnpackMaterial();

        if (unpackMaterial != null)
        {
            Graphics.Blit(source, rt, unpackMaterial);
            DestroyImmediate(unpackMaterial);
        }
        else
        {
            Graphics.Blit(source, rt);
        }

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D readableTex = new Texture2D(
            targetSize,
            targetSize,
            TextureFormat.RGBA32,
            false,
            true
        );

        readableTex.ReadPixels(
            new Rect(0, 0, targetSize, targetSize),
            0,
            0
        );

        readableTex.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        Color[] pixels = readableTex.GetPixels();

        DestroyImmediate(readableTex);

        return pixels;
    }

    private Material GetNormalUnpackMaterial()
    {
        Shader unpackShader = Shader.Find("Hidden/UnpackNormal");

        if (unpackShader == null)
        {
            string shaderCode = @"
Shader ""Hidden/UnpackNormalCustom""
{
    Properties
    {
        _MainTex (""Texture"", 2D) = ""white"" {}
    }

    SubShader
    {
        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include ""UnityCG.cginc""

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float3 normal = UnpackNormal(tex);

                return fixed4(
                    normal.x * 0.5 + 0.5,
                    normal.y * 0.5 + 0.5,
                    normal.z * 0.5 + 0.5,
                    1.0
                );
            }

            ENDCG
        }
    }
}";

            unpackShader = ShaderUtil.CreateShaderAsset(shaderCode);
        }

        if (unpackShader != null)
            return new Material(unpackShader);

        return null;
    }

    private void SaveOrReplaceAsset(
        UnityEngine.Object newAsset,
        string assetPath
    )
    {
        newAsset.name = Path.GetFileNameWithoutExtension(assetPath);

        UnityEngine.Object existingAsset =
            AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);

        if (existingAsset != null)
        {
            EditorUtility.CopySerialized(newAsset, existingAsset);
            DestroyImmediate(newAsset);
        }
        else
        {
            AssetDatabase.CreateAsset(newAsset, assetPath);
        }
    }
}