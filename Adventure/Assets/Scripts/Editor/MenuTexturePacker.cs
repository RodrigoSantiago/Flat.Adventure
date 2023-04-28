using UnityEditor;
using UnityEngine;

public class MenuTexturePacker : EditorWindow {

    private Texture2D normalTexture;
    private Texture2D smoothnessTexture;
    private Texture2D metallicTexture;
    private Texture2D colorTexture;
    private Texture2D aoTexture;
    private Texture2D emTexture;

    [MenuItem("Texture Tools/Pack Textures")]
    static void Init() {
        GetWindow(typeof(MenuTexturePacker)).Show();
    }

    void OnGUI() {
        GUILayout.Label("Pack Textures", EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Color Texture:");
        colorTexture = (Texture2D)EditorGUILayout.ObjectField(colorTexture, typeof(Texture2D), false, GUILayout.Width(100), GUILayout.Height(100));
        GUILayout.EndHorizontal();
        
        GUILayout.BeginHorizontal();
        GUILayout.Label("Ambient Occlusion Texture:");
        aoTexture = (Texture2D)EditorGUILayout.ObjectField(aoTexture, typeof(Texture2D), false, GUILayout.Width(100), GUILayout.Height(100));
        GUILayout.EndHorizontal();
        
        GUILayout.BeginHorizontal();
        GUILayout.Label("Emission Texture:");
        emTexture = (Texture2D)EditorGUILayout.ObjectField(emTexture, typeof(Texture2D), false, GUILayout.Width(100), GUILayout.Height(100));
        GUILayout.EndHorizontal();
        
        if (GUILayout.Button("Pack Color")) {
            string savePath = EditorUtility.SaveFilePanel("Save Packed Texture", "", "packed_texture", "png");

            if (savePath.Length > 0) {
                // Create the packed texture
                Texture2D packedTexture = new Texture2D(normalTexture.width, normalTexture.height, TextureFormat.RGBA32, false);
                // ----- B
                // Get the texture arrays
                Color[] colorPixels = colorTexture.GetPixels();
                Color[] aoPixels = aoTexture == null ? null : aoTexture.GetPixels();
                Color[] emPixels = emTexture == null ? null : emTexture.GetPixels();
                Color[] packedPixels = new Color[colorPixels.Length];

                // Pack the textures into the packed texture
                for (int i = 0; i < colorPixels.Length; i++) {
                    // Get the R and G channels from the normal texture
                    float r = colorPixels[i].r;
                    float g = colorPixels[i].g;
                    float b = colorPixels[i].b;

                    // Get the B channel from the smoothness texture
                    float e = emPixels == null ? 0 : emPixels[i].r;

                    // Get the A channel from the metallic texture
                    float a = emPixels == null ? 0 : 1 - aoPixels[i].r;

                    // Pack the channels into a single color
                    packedPixels[i] = new Color(r, g, b, (e - a) * 0.5f + 0.5f);
                }
                
                Debug.Log("Pixels Converted");

                // Set the pixels in the packed texture
                packedTexture.SetPixels(packedPixels);
                packedTexture.Apply();
                
                Texture2D resizedTexture = new Texture2D(512, 512, TextureFormat.RGBA32, false);
                Graphics.ConvertTexture(packedTexture, resizedTexture);
                resizedTexture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                var bytes = resizedTexture.EncodeToPNG();
                Debug.Log("Resize");
                
                System.IO.File.WriteAllBytes(savePath, bytes);
                Debug.Log("Written");
                AssetDatabase.Refresh();
            }
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label("Normal Texture:");
        normalTexture = (Texture2D)EditorGUILayout.ObjectField(normalTexture, typeof(Texture2D), false, GUILayout.Width(100), GUILayout.Height(100));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Smoothness Texture:");
        smoothnessTexture = (Texture2D)EditorGUILayout.ObjectField(smoothnessTexture, typeof(Texture2D), false, GUILayout.Width(100), GUILayout.Height(100));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Metallic Texture:");
        metallicTexture = (Texture2D)EditorGUILayout.ObjectField(metallicTexture, typeof(Texture2D), false, GUILayout.Width(100), GUILayout.Height(100));
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Analise")) {
            Color[] normalPixels = normalTexture.GetPixels();
            float total = 0, m = 0;
            for (int i = 0; i < normalPixels.Length; i++) {
                float r = normalPixels[i].r;
                float g = normalPixels[i].g;
                float b = normalPixels[i].b;
                r *= 2 - 1;
                g *= 2 - 1;
                b *= 2 - 1;
                float bb = Mathf.Sqrt(1 - r * r - g * g);
                float dist = Mathf.Sqrt(r * r + g * g + b * b);
                float dist2 = Mathf.Sqrt(r * r + g * g + bb * bb);
                if (Mathf.Abs(dist - 1) > 0.1) {
                    total += 1;
                    m += Mathf.Abs(dist - 1);
                    //Debug.Log(normalPixels[i].r+", "+normalPixels[i].g+","+normalPixels[i].b + " :: " + Mathf.Sqrt(dist));
                }
            }
            Debug.Log(total+"/"+normalPixels.Length+"="+(total/normalPixels.Length));
            Debug.Log(m/normalPixels.Length);
        }
        if (GUILayout.Button("Pack Map")) {
            string savePath = EditorUtility.SaveFilePanel("Save Packed Texture", "", "packed_texture", "png");

            if (savePath.Length > 0 && normalTexture != null) {
                // Create the packed texture
                Texture2D packedTexture = new Texture2D(normalTexture.width, normalTexture.height, TextureFormat.RGBA32, false);

                // Get the texture arrays
                Color[] normalPixels = normalTexture.GetPixels();
                Color[] smoothnessPixels = smoothnessTexture == null ? null : smoothnessTexture.GetPixels();
                Color[] metallicPixels = metallicTexture == null ? null : metallicTexture.GetPixels();
                Color[] packedPixels = new Color[normalPixels.Length];

                // Pack the textures into the packed texture
                for (int i = 0; i < normalPixels.Length; i++) {
                    // Get the R and G channels from the normal texture
                    Vector3 normal = new Vector3(normalPixels[i].r, normalPixels[i].g, normalPixels[i].b);
                    normal = normal.normalized;
                    float r = normal.x;
                    float g = normal.y;

                    // Get the B channel from the smoothness texture
                    float b = smoothnessPixels == null ? 0 : smoothnessPixels[i].r;

                    // Get the A channel from the metallic texture
                    float a = metallicPixels == null ? 1 : 1 - metallicPixels[i].r;

                    // Pack the channels into a single color
                    packedPixels[i] = new Color(r, g, b, a);
                }
                Debug.Log("Pixels Converted");

                // Set the pixels in the packed texture
                packedTexture.SetPixels(packedPixels);
                packedTexture.Apply();

                // Resize the packed texture to 512x512
                Texture2D resizedTexture = new Texture2D(512, 512, TextureFormat.RGBA32, false);
                Graphics.ConvertTexture(packedTexture, resizedTexture);
                resizedTexture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                byte[] bytes = resizedTexture.EncodeToPNG();
                Debug.Log("Resize");

                // Apply changes and save the packed texture
                System.IO.File.WriteAllBytes(savePath, bytes);
                Debug.Log("Written");
                AssetDatabase.Refresh();
            }
        }
    }
}