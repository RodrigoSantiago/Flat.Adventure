using UnityEngine;

namespace Code {

    public class MeshNormalDebugDrawer : MonoBehaviour {
        [SerializeField] private float length = 0.25f;
        [SerializeField] private Color color = Color.green;
        [SerializeField] private bool onlyFacingCamera = true;

        private Camera GetDebugCamera() {
            if (!Application.isPlaying) {
                var sceneView = UnityEditor.SceneView.lastActiveSceneView;

                if (sceneView != null)
                    return sceneView.camera;
            }

            return Camera.main;
        }

        private void Update() {
            DrawNormals();
        }

        private void DrawNormals() {
            if (MeshDebugData.Vertices == null ||
                MeshDebugData.Normals == null)
                return;

            Camera cam = GetDebugCamera();

            for (int i = 0; i < MeshDebugData.Vertices.Length; i++) {
                Vector3 pos = transform.TransformPoint(
                    MeshDebugData.Vertices[i]
                );

                Vector3 normal = transform.TransformDirection(
                    MeshDebugData.Normals[i]
                ).normalized;

                Debug.DrawLine(
                    pos,
                    pos + normal * length,
                    color
                );
            }
        }
    }
}