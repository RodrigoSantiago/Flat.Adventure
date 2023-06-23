using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public class BlendTreeAssetSaver {
    [MenuItem("Assets/Save Blend Tree")]
    private static void SaveBlendTree() {
        // Get the active BlendTree from the animator controller
        BlendTree blendTree = Selection.activeObject as BlendTree;

        if (blendTree == null) {
            Debug.LogError("Please select a BlendTree asset.");
            return;
        }

        // Create a copy of the BlendTree
        BlendTree blendTreeCopy = Object.Instantiate(blendTree);

        // Set the path and name for the new asset
        string path = EditorUtility.SaveFilePanelInProject("Save BlendTree Asset", blendTree.name, "asset", "Save BlendTree Asset");

        if (string.IsNullOrEmpty(path))
            return;

        // Create the asset
        AssetDatabase.CreateAsset(blendTreeCopy, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("BlendTree asset saved: " + path);
    }
}