using Game.Worlds;

namespace Game.DebugUtils {
    using UnityEngine;

namespace Game.Utils {
    public class FrustumShadowDebug : MonoBehaviour {
        [Header("Configurações de Debug")]
        [Tooltip("Cor base dos Frustums de Sombra")]
        public Color shadowFrustumColor = Color.yellow;
        
        [Tooltip("Desenhar Gizmos apenas quando o objeto estiver selecionado?")]
        public bool drawOnlyWhenSelected = false;

        private void OnDrawGizmos() {
            if (!drawOnlyWhenSelected) {
                DrawShadowFrustums();
            }
        }

        private void OnDrawGizmosSelected() {
            if (drawOnlyWhenSelected) {
                DrawShadowFrustums();
            }
        }

        private void DrawShadowFrustums() {
            // Acessa a sua estrutura estática temporária
            var planesPerLod = WorldRender.frustumShadowPlanes;
            if (planesPerLod == null) return;

            for (int lod = 0; lod < planesPerLod.Length; lod++) {
                Plane[] planes = planesPerLod[lod];
                
                // Valida se os planos deste LOD já foram inicializados
                if (planes == null || planes.Length < 6) continue;

                // Alterna a opacidade da cor por LOD para diferenciar visualmente na Scene View
                Color lodColor = shadowFrustumColor;
                lodColor.a = Mathf.Lerp(1.0f, 0.25f, (float)lod / planesPerLod.Length);
                Gizmos.color = lodColor;

                // Tenta calcular os 8 vértices a partir das intersecções dos 6 planos
                if (TryComputeFrustumCorners(planes, out Vector3[] corners)) {
                    DrawFrustumLines(corners);
                }
            }
        }

        /// <summary>
        /// Calcula os 8 vértices da pirâmide/frustum resolvendo a intersecção de 3 planos por vértice.
        /// Ordem dos planos em GeometryUtility: 0=Left, 1=Right, 2=Down, 3=Up, 4=Near, 5=Far
        /// </summary>
        private bool TryComputeFrustumCorners(Plane[] planes, out Vector3[] corners) {
            corners = new Vector3[8];

            // 4 Vértices do Near Plane (Perto)
            if (!Intersection3Planes(planes[4], planes[0], planes[2], out corners[0])) return false; // Near - Left - Down
            if (!Intersection3Planes(planes[4], planes[1], planes[2], out corners[1])) return false; // Near - Right - Down
            if (!Intersection3Planes(planes[4], planes[1], planes[3], out corners[2])) return false; // Near - Right - Up
            if (!Intersection3Planes(planes[4], planes[0], planes[3], out corners[3])) return false; // Near - Left - Up

            // 4 Vértices do Far Plane (Longe)
            if (!Intersection3Planes(planes[5], planes[0], planes[2], out corners[4])) return false; // Far - Left - Down
            if (!Intersection3Planes(planes[5], planes[1], planes[2], out corners[5])) return false; // Far - Right - Down
            if (!Intersection3Planes(planes[5], planes[1], planes[3], out corners[6])) return false; // Far - Right - Up
            if (!Intersection3Planes(planes[5], planes[0], planes[3], out corners[7])) return false; // Far - Left - Up

            return true;
        }

        /// <summary>
        /// Desenha as 12 arestas do tronco de pirâmide no Gizmos
        /// </summary>
        private void DrawFrustumLines(Vector3[] c) {
            // Retângulo do Near Plane
            Gizmos.DrawLine(c[0], c[1]);
            Gizmos.DrawLine(c[1], c[2]);
            Gizmos.DrawLine(c[2], c[3]);
            Gizmos.DrawLine(c[3], c[0]);

            // Retângulo do Far Plane
            Gizmos.DrawLine(c[4], c[5]);
            Gizmos.DrawLine(c[5], c[6]);
            Gizmos.DrawLine(c[6], c[7]);
            Gizmos.DrawLine(c[7], c[4]);

            // 4 Arestas Conectando Near -> Far
            Gizmos.DrawLine(c[0], c[4]);
            Gizmos.DrawLine(c[1], c[5]);
            Gizmos.DrawLine(c[2], c[6]);
            Gizmos.DrawLine(c[3], c[7]);
        }

        /// <summary>
        /// Resolve P = (d1*(N2 x N3) + d2*(N3 x N1) + d3*(N1 x N2)) / det
        /// </summary>
        private bool Intersection3Planes(Plane p1, Plane p2, Plane p3, out Vector3 intersection) {
            intersection = Vector3.zero;

            Vector3 n1 = p1.normal;
            Vector3 n2 = p2.normal;
            Vector3 n3 = p3.normal;

            // d na equação da Plane do Unity é a distância ax + by + cz + d = 0, então usamos -distance
            float d1 = -p1.distance;
            float d2 = -p2.distance;
            float d3 = -p3.distance;

            Vector3 n2xn3 = Vector3.Cross(n2, n3);
            float det = Vector3.Dot(n1, n2xn3);

            // Se o determinante for muito próximo de 0, os planos são paralelos
            if (Mathf.Abs(det) < 1e-5f) return false;

            Vector3 n3xn1 = Vector3.Cross(n3, n1);
            Vector3 n1xn2 = Vector3.Cross(n1, n2);

            intersection = (d1 * n2xn3 + d2 * n3xn1 + d3 * n1xn2) / det;
            return true;
        }
    }
}
}