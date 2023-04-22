using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChunkPreview : MonoBehaviour {
    
    public Renderer previeRender;
    public MeshFilter meshRenderer;

    public MapPreview map;

    public int x;
    public int y;
    public int size; // 16
    public int d; // 32 = 32 x 16
    public float level = 1;
    
    
    private Vector3[] vertices;
    private Color[] colors;
    private int[] indices;
    private Mesh mesh;
    private int vW, vH;

    public void Remesh(bool colorOnly) {
        vW = size + 1;
        vH = size + 1;
        if (vertices == null || vertices.Length != vW * vH) {
            vertices = new Vector3[vW * vH];
            colors = new Color[vW * vH];
            indices = new int[6 * (vW - 1) * (vH - 1)];
            colorOnly = false;
            int i = 0;
            for (int x = 0; x < (vW - 1); x++) {
                for (int y = 0; y < (vH - 1); y++) {
                    indices[i++] = x + y * vW;
                    indices[i++] = x + (y + 1) * vW;
                    indices[i++] = x + 1 + (y + 1) * vW;

                    indices[i++] = x + y * vW;
                    indices[i++] = x + 1 + (y + 1) * vW;
                    indices[i++] = x + 1 + y * vW;
                }
            }
            mesh = new Mesh();
            meshRenderer.mesh = mesh;
        }

        for (int x = 0; x < vW; x++) {
            for (int y = 0; y < vH; y++) {
                Color c = map.getColor(this.x + x * level, this.y + y * level);
                if (!colorOnly) {
                    vertices[x + y * vW] = new Vector3(x / (float)size * level, c.a * d, y / (float)size * level);
                }

                c.a = 1;
                colors[x + y * vW] = c;
            }
        }

        if (!colorOnly) {
            mesh.vertices = vertices;
            mesh.triangles = indices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
        mesh.colors = colors;
        //mesh.RecalculateTangents();
    }
}
