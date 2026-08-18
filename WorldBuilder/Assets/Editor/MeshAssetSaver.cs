using UnityEngine;
using UnityEditor;

public static class MeshAssetSaver
{
    [MenuItem("Tools/Mesh/Save Selected Mesh As Asset")]
    public static void SaveSelectedMeshAsAsset()
    {
        GameObject selected = Selection.activeGameObject;

        if (selected == null)
        {
            Debug.LogWarning("Selecione um GameObject com MeshFilter.");
            return;
        }

        MeshFilter meshFilter = selected.GetComponent<MeshFilter>();

        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogWarning("O GameObject selecionado não possui um MeshFilter com Mesh.");
            return;
        }

        Mesh originalMesh = meshFilter.sharedMesh;

        string path = EditorUtility.SaveFilePanelInProject(
            "Salvar Mesh como Asset",
            originalMesh.name + ".asset",
            "asset",
            "Escolha onde salvar o Mesh."
        );

        if (string.IsNullOrEmpty(path))
            return;

        // Cria uma cópia para não modificar o mesh original.
        Mesh meshCopy = Object.Instantiate(originalMesh);
        meshCopy.name = originalMesh.name;

        AssetDatabase.CreateAsset(meshCopy, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Mesh salvo como Asset: {path}");
    }

    // Atalho disponível somente quando há um MeshFilter válido selecionado.
    [MenuItem("Tools/Mesh/Save Selected Mesh As Asset", true)]
    private static bool ValidateSaveSelectedMeshAsAsset()
    {
        GameObject selected = Selection.activeGameObject;

        if (selected == null)
            return false;

        MeshFilter meshFilter = selected.GetComponent<MeshFilter>();

        return meshFilter != null && meshFilter.sharedMesh != null;
    }
}