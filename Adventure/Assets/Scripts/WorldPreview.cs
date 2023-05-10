using System;
using System.Collections;
using System.Collections.Generic;
using Adventure.Logic.Data;
using Adventure.Logic;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public class WorldPreview : MonoBehaviour {
    
    
    public GameObject prefabGrass;
    public MeshFilter meshRenderer;
    
    public int width;
    public int length;
    public int height;
    [Range(1, 100)]
    public float scale = 50;
    [Range(1, 100)]
    public float bScale = 50;
    [Range(0, 2)]
    public float frac = 1;
    [Range(1, 10)]
    public float offsetX = 1;
    [Range(1, 10)]
    public float offsetY = 1;
    private float prev = 0;
    private float prevS = 0;
    private float prevX = 0;
    private float prevY = 0;
    private float prevF = 0;
    private float invalid;

    private Mesh mesh;
    public Texture2D texture;
    private Vector3[] vertices;
    private Vector2[] uvs;
    private int[] indices;
    private Color[] colors;

    void Start() {
        mesh = new Mesh();
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.Space)) {
            createWorld();
        }

        if (prev != scale || prevX != offsetX || prevY != offsetY || prevS != bScale || frac != prevF) {
            invalid = 1;
        }

        if (invalid > 0) {
            invalid -= Time.deltaTime * 10;
            if (invalid < 0) {
                createWorld();
            }
        }
        prev = scale;
        prevS = bScale;
        prevX = offsetX;
        prevY = offsetY;
        prevF = frac;
    }

    public void destroyWorld() {
        foreach (var obj in GameObject.FindGameObjectsWithTag("Respawn")) {
            Destroy(obj);
        }
    }

    private void TestPerlimRandomness() {
        double min = 5;
        double max = -5;
        int n = 10000000;
        Random random = new Random(886389552);
        for (int i = 0; i < n; i++) {
            double f = noise.Wave3D(random.NextDouble(1000), random.NextDouble(1000), random.NextDouble(1000));
            if (f < min) min = f;
            if (f > max) max = f;
        }
        Debug.Log("Min : " + min);
        Debug.Log("Max : " + max);
    }

    private void TestRandomness() {
        double min = 5;
        double max = -5;
        int n = 1000000;
        int col = 0;
        HashSet<int> hash = new HashSet<int>();
        int[] between = new int[100];
        for (int i = 0; i < n; i++) {
            int l = noise.RandomInt(i);
            if (!hash.Add(l)) col++;
            double f = noise.Random(i);
            for (int j = 0; j < between.Length; j++) {
                if (f >= j / (double) between.Length && f < (j + 1) / (double) between.Length) {
                    between[j]++;
                    break;
                }
            }

            if (f < min) min = f;
            if (f > max) max = f;
        }

        Debug.Log("Min : " + min);
        Debug.Log("Max : " + max);
        Debug.Log("Different : " + hash.Count + " = " + (hash.Count / (double) n));
        Debug.Log("Conflicts : " + col + " = " + (col / (double) n));
        for (int i = 0; i < between.Length; i++) {
            Debug.Log("P" + i + " : " + (between[i] / (float)n * between.Length));
        }
    }

    public void createWorld() {
        destroyWorld();
        
        if (vertices == null || vertices.Length < width * length) {
            vertices = new Vector3[width * length];
            uvs = new Vector2[width * length];
            indices = new int[6 * (width - 1) * (length - 1)];
            colors = new Color[width * length];
            texture = new Texture2D(width, length);
            texture.filterMode = FilterMode.Point;
            GetComponent<Renderer>().material.mainTexture = texture;
            int i = 0;
            for (int x = 0; x < width; x++) {
                for (int y = 0; y < length; y++) {
                    uvs[x + y * width] = new Vector2(1f / width * x, 1f / length * y);
                }
            }

            for (int x = 0; x < width - 1; x++) {
                for (int y = 0; y < length - 1; y++) {
                    indices[i++] = x + y * width;
                    indices[i++] = x + (y + 1) * width;
                    indices[i++] = x + 1 + (y + 1) * width;
                    
                    indices[i++] = x + y * width;
                    indices[i++] = x + 1 + (y + 1) * width;
                    indices[i++] = x + 1 + y * width;
                }
            }
        }

        /*for (int x = 0; x < width; x++) {
            for (int y = 0; y < length; y++) {
                for (int z = 0; z < height; z++) {
                    if (createVoronoiVoxel(x, y, z)) {
                        var obj = Instantiate(prefabGrass, new Vector3(x, z, y), Quaternion.identity);
                        obj.SetActive(true);
                        obj.tag = "Respawn";
                    }
                }
            }
        }*/

        for (int x = 0; x < width; x++) {
            for (int y = 0; y < length; y++) {
                vertices[x + y * width] = create(x, y);
                colors[x + y * width] = createColoVoronoi(x, y);
            }
        }
        texture.SetPixels(colors);
        texture.Apply();
        mesh.vertices = vertices;
        mesh.triangles = indices;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        meshRenderer.mesh = mesh;
    }

    private bool createVoronoiVoxel(int x, int y, int z) {
        float n = getVoronoiBorder3D(x / scale, y / scale, z / scale, frac);
        return n > bScale / 50;
    }

    private Color createColoVoronoi(int x, int y) {
        /*float[] distance = {0, 1};
        distance[1] = getVoronoi(x / scale, y / scale, frac, distance);
        float n = distance[1] < 0.1 ? 1 : mix(0, Mathf.Pow(Mathf.Pow(distance[1], 6), 6), 1, Mathf.Pow(Mathf.Pow(distance[0], 6), 6));
        float t = (n - 0.475f) * 40;
        return new Color(t, t, t, 1);*/
        float n = getVoronoiBorder(x / scale, y / scale, frac);
        float t = n < bScale / 50 ? 0 : 1;
        return new Color(t, t, t, 1);
    }

    private Color createColorPerlim(int x, int y) {
        float n = PerlinNoise(x / scale, y / scale);
        float t = (n * 20) % 1.0f;
        if ((int) (n * 20) % 2 == 1) t = 1 - t;
        t = Interpolation.Exp10(t);
        return new Color(t, t, t, 1);
    }

    private Noise noise = new Noise(716318351658410L);
    private float PerlinNoise(float x, float y) {
        return (float) noise.Wave2D(x, y);
    }
    
    private Vector3 create(int x, int y) {
        Vector3 position = new Vector3(x, 0, y);
        float h = PerlinNoise(x / scale, y / scale) * 2 - 1f;
        float h2 = PerlinNoise(x / scale / 2, y / scale / 2) * 2 - 1f;
        float h3 = PerlinNoise(x / scale / 4, y / scale / 4) * 2 - 1f;
        h = h + h2 * 0.5f + h3 * 0.25f;
        //position.y = h * height;

        /*fn.SetCellularReturnType(FastNoiseLite.CellularReturnType.Distance);
        fn.SetCellularDistanceFunction(FastNoiseLite.CellularDistanceFunction.Euclidean);
        fn.SetNoiseType(FastNoiseLite.NoiseType.Cellular);
        position.y = height * fn.GetNoise(x / scale, y / scale);
        int[] cells = new int[3];
        float[] distance = {999, 999, 999};
        getVoronoi(x * scale + offsetX, y * scale + offsetY, 1, cells, distance);
        Vector3 position = new Vector3(x, 0, y);
        position.y = distance[0] * height;*/
        return position;
    }

    private Color mix(Color colorA, float wA, Color colorB, float wB) {
        return (colorA * wA + colorB * wB) / (wA + wB);
    }

    private float mix(float colorA, float wA, float colorB, float wB) {
        return (colorA * wA + colorB * wB) / (wA + wB);
    }

    private Color mix(Color colorA, float wA, Color colorB, float wB, Color colorC, float wC) {
        return (colorA * wA + colorB * wB + colorC * wC) / (wA + wB + wC);
    }

    private float mix(float colorA, float wA, float colorB, float wB, float colorC, float wC) {
        return (colorA * wA + colorB * wB + colorC * wC) / (wA + wB + wC);
    }

    private Color createColor(int x, int y) {
        
        int[] cells = new int[3];
        float[] distance = {999, 999, 999};
        getVoronoi(x / bScale + offsetX, y / bScale + offsetY, frac, cells, distance);
        Random r = new Random((uint) cells[0]);
        Random b = new Random((uint) cells[1]);
        Random c = new Random((uint) cells[2]);
        Color rColor = new Color(r.NextFloat(), r.NextFloat(), r.NextFloat(), 1f);
        Color bColor = new Color(b.NextFloat(), b.NextFloat(), b.NextFloat(), 1f);
        Color cColor = new Color(c.NextFloat(), c.NextFloat(), c.NextFloat(), 1f);
        Color eColor = rColor;
        if (distance[0] <= 0.05f) {
            eColor = Color.black;
        }else if (distance[0] > 0.001f) {
            eColor = mix(rColor, 1 / Mathf.Pow(distance[0]*10, 12), bColor, 1 / Mathf.Pow(distance[1]*10, 12), cColor, 1 / Mathf.Pow(distance[2]*10, 12));
        }

        return eColor;
    }

    private Vector3 getFractalCell(Vector3 cell, float fractal) {
        float perlim = Mathf.Clamp01(PerlinNoise(cell.x * Mathf.PI + 0.12684f, cell.y * Mathf.PI - 0.68416f)) * Mathf.PI * 2;
        return new Vector3(cell.x + Mathf.Cos(perlim) * fractal, cell.y + Mathf.Sin(perlim) * fractal, cell.z + Mathf.Sin(perlim) * fractal);
    }

    private Vector2 getFractalCell(Vector2 cell, float fractal) {
        float perlim = Mathf.Clamp01(PerlinNoise(cell.x * Mathf.PI + 0.12684f, cell.y * Mathf.PI - 0.68416f)) * Mathf.PI * 2;
        return new Vector2(cell.x + Mathf.Cos(perlim) * fractal, cell.y + Mathf.Sin(perlim) * fractal);
    }

    private void getVoronoi(float x, float y, float fractal, int[] cells, float[] distance) {
        Vector2 p = new Vector2(Mathf.Floor(x), Mathf.Floor(y));
        Vector2 f = getFractalCell(new Vector2(x, y), fractal * 0.1f);

        for (int j = -1; j <= 1; j++) {
            for (int i = -1; i <= 1; i++) {
                Vector2 cell = getFractalCell(p + new Vector2(i, j), fractal);
                Vector2 r = cell - f;
                float d = r.x * r.x + r.y * r.y;

                if (d < distance[0]) {
                    distance[2] = distance[1];
                    distance[1] = distance[0];
                    distance[0] = d;
                    cells[2] = cells[1];
                    cells[1] = cells[0];
                    cells[0] = cell.GetHashCode();
                } else if (d < distance[1]) {
                    distance[2] = distance[1];
                    distance[1] = d;
                    cells[2] = cells[1];
                    cells[1] = cell.GetHashCode();
                } else if (d < distance[2]) {
                    distance[2] = d;
                    cells[2] = cell.GetHashCode();
                }
            }
        }

        distance[0] = Mathf.Sqrt(distance[0]);
        distance[1] = Mathf.Sqrt(distance[1]);
        distance[2] = Mathf.Sqrt(distance[2]);
    }

    private float getVoronoiBorder3D(float x, float y, float z, float fractal) {
        Vector3 p = new Vector3(Mathf.Floor(x), Mathf.Floor(y), Mathf.Floor(z));
        Vector3 f = new Vector3(x, y, z);

        float dx = 8, dy = 8, dz = 8;
        float min = 9999;
        for (int j = -1; j <= 1; j++) {
            for (int i = -1; i <= 1; i++) {
                for (int k = -1; k <= 1; k++) {
                    Vector3 cell = getFractalCell(p + new Vector3(i, j, k), fractal);
                    Vector3 r = cell - f;
                    dz = r.magnitude;
                    dy = Mathf.Max(dx, smin(dy, dz, 0.4f));
                    dx = smin(dx, dz, 0.2f);
                }
            }
        }

        return dy - dx;
    }

    private float getVoronoiBorder(float x, float y, float fractal) {
        Vector2 p = new Vector2(Mathf.Floor(x), Mathf.Floor(y));
        Vector2 f = new Vector2(x, y);

        float dx = 8, dy = 8, dz = 8;
        for (int j = -2; j <= 2; j++) {
            for (int i = -2; i <= 2; i++) {
                Vector2 cell = getFractalCell(p + new Vector2(i, j), fractal);
                Vector2 r = cell - f;
                
                dz = r.magnitude; 
                dy = Mathf.Max(dx, smin(dy, dz, 0.4f));
                dx = smin(dx, dz, 0.2f); 
            }
        }

        return dy - dx;
    }

    private float getVoronoi(float x, float y, float fractal, float[] distance) {
        Vector2 p = new Vector2(Mathf.Floor(x), Mathf.Floor(y));
        Vector2 f = new Vector2(x, y);

        float res = 0;
        float dist = 9999;
        for (int j = -2; j <= 2; j++) {
            for (int i = -2; i <= 2; i++) {
                Vector2 cell = getFractalCell(p + new Vector2(i, j), fractal);
                Vector2 r = cell - f;
                float d = r.x * r.x + r.y * r.y;

                res += 1.0f/Mathf.Pow( d, 8.0f);
                if (d < dist) {
                    dist = d;
                }
            }
        }

        distance[0] = Mathf.Pow( 1.0f/res, 1.0f/16.0f);
        return Mathf.Sqrt(dist);
    }

    private float smin(float a, float b, float k) {
        float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
        return Mathf.Lerp(b, a, h) - k * h * (1.0f - h);
    }
}
