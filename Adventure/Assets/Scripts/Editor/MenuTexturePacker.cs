using UnityEditor;
using UnityEngine;

public class MenuTexturePacker : EditorWindow {

    private Texture2D normalTexture;
    private Texture2D smoothnessTexture;
    private Texture2D metallicTexture;

    [MenuItem("Texture Tools/Pack Textures")]
    static void Init() {
        GetWindow(typeof(MenuTexturePacker)).Show();
    }

    void OnGUI() {
        GUILayout.Label("Pack Textures", EditorStyles.boldLabel);

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

        if (GUILayout.Button("Pack")) {
            string savePath = EditorUtility.SaveFilePanel("Save Packed Texture", "", "packed_texture", "png");

            if (savePath.Length > 0 && normalTexture != null && smoothnessTexture != null && metallicTexture != null) {
                // Create the packed texture
                Texture2D packedTexture = new Texture2D(normalTexture.width, normalTexture.height, TextureFormat.RGBA32, false);

                // Get the texture arrays
                Color[] normalPixels = normalTexture.GetPixels();
                Color[] smoothnessPixels = smoothnessTexture.GetPixels();
                Color[] metallicPixels = metallicTexture.GetPixels();
                Color[] packedPixels = new Color[normalPixels.Length];

                // Pack the textures into the packed texture
                for (int i = 0; i < normalPixels.Length; i++) {
                    // Get the R and G channels from the normal texture
                    float r = normalPixels[i].r;
                    float g = normalPixels[i].g;

                    // Get the B channel from the smoothness texture
                    float b = smoothnessPixels[i].r;

                    // Get the A channel from the metallic texture
                    float a = 1 - metallicPixels[i].r;

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