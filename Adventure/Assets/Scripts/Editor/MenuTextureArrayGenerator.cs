#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

public class MenuTextureArrayGenerator : EditorWindow {
    SerializedObject serialO;
    SerializedProperty texturesSerialized;

    public Texture2D[] textures;
    public Object saveDir;
    private string assetsDir;


    private void OnEnable() {
        assetsDir = Application.dataPath;
        serialO = new SerializedObject(this);
        texturesSerialized = serialO.FindProperty("textures");

        saveDir = AssetDatabase.LoadAssetAtPath<Object>("Assets");
    }

    [MenuItem("Custom Tools/Texture Array Generator")]
    public static void Init() {
        GetWindow(typeof(MenuTextureArrayGenerator), false, "Texture Array Generator");
    }

    private void OnGUI() {
        serialO.Update();

        EditorGUILayout.PropertyField(texturesSerialized, true);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Result Texture:");
        GUILayout.EndHorizontal();
        serialO.ApplyModifiedProperties();

        if (GUILayout.Button("Generate Array")) {
        
            string savePath = EditorUtility.SaveFilePanel("Save Array Texture", "", "TexArray", "asset");
            if (savePath.Length > 0) {
                Texture2D temp = new Texture2D(512, 512, TextureFormat.RGBA32, true);
                int texturesCount = textures.Length;

                Texture2DArray array = new Texture2DArray(512, 512, texturesCount, TextureFormat.RGBA32, true);

                for (int t = 0; t < texturesCount; t++) {
                    Texture2D currentTexture = textures[t];
                    Graphics.ConvertTexture(currentTexture, temp);
                    temp.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                    
                    for (int mipMapLevel = 0; mipMapLevel < temp.mipmapCount; mipMapLevel++) {
                        Graphics.CopyTexture(temp, 0, mipMapLevel, array, t, mipMapLevel);
                    };
                }
                
                savePath = savePath.Substring(savePath.IndexOf("Assets/"));
                if (!savePath.EndsWith(".asset")) savePath += ".asset";
                AssetDatabase.CreateAsset(array, savePath);
            }
        }
    }
}

#endif