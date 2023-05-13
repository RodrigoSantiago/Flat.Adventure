using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MeshFilter))]
public class DebugTool_ShowNormal : Editor 
{
    
    private const string     EDITOR_PREF_KEY = "_normals_length";
    private const string     EDITOR_PREF_BOOL = "_show_normals";
    private       Mesh       mesh;
    private       MeshFilter mf;
    private       float      normalsLength = 1f;
    private       bool       showNormals = false;

    private void OnEnable() {
        mf   = target as MeshFilter;
        if (mf != null) {
            mesh = mf.sharedMesh;
        }
        normalsLength = EditorPrefs.GetFloat(EDITOR_PREF_KEY);
        showNormals = EditorPrefs.GetBool(EDITOR_PREF_BOOL);
    }

    private void OnSceneGUI() {
        if (mesh == null || !showNormals) {
            return;
        }

        Handles.matrix = mf.transform.localToWorldMatrix;
        Handles.color = Color.yellow;
        var verts = mesh.vertices;
        var normals = mesh.normals;
        int len = mesh.vertexCount;
        
        for (int i = 0; i < len; i++) {
            Handles.DrawLine(verts[i], verts[i] + normals[i] * normalsLength);
        }
    }

    public override void OnInspectorGUI() {
        base.OnInspectorGUI();
        EditorGUI.BeginChangeCheck();
        showNormals = EditorGUILayout.Toggle("Show normals", showNormals);
        normalsLength = EditorGUILayout.FloatField("Normals length", normalsLength);
        if (EditorGUI.EndChangeCheck()) {
            EditorPrefs.SetBool(EDITOR_PREF_BOOL, showNormals);
            EditorPrefs.SetFloat(EDITOR_PREF_KEY, normalsLength);
        }
    }
}