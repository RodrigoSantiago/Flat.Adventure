using System;
using System.Collections;
using System.Collections.Generic;
using Adventure.Data;
using Adventure.Logic;
using UnityEngine;

public class EquationsPreview : MonoBehaviour {

    public enum ContinentalType {
        OVAL, SQUARE, DOUBLE_OVAL, DOUBLE_SQUARE
    }
    public Renderer previeRender;
    public MeshFilter meshRenderer;
    public int width = 2048;
    public int height = 1024;
    public long seed = 65198456164L;
    public bool build;
    private bool prevBuild;
    public float islandsScale = 200;
    public float islandsMin = 0.66f;
    public float mainScale = 1;
    public float mainPower = 1;
    public float secondScale = 1;
    public float secondPower = 1;
    public float thridScale = 1;
    public float thirdPower = 1;
    public float smoothContinental = 0.75f;
    public ContinentalType continentalType;
    public float cutOut = 0.5f;
    public float cutOutBeach = 0.45f;
    public bool smooth;
    public float biomeScale = 1;
    public float biomeNoise = 0.1f;
    public float biomeNoiseScale = 1;
    public float biomeSecondNoise = 0.1f;
    public float biomeSecondNoiseScale = 1;
    private Texture2D texture;
    private Color[] colors;

    private Noise noise;
    private Mesh mesh;
    private Vector3[] vertices;
    private Vector2[] uvs;
    private int[] indices;
    private World World;
    
    void Start() {
        World = new World(seed, this.width, this.height, 100);

        colors = new Color[this.width * this.height];
        texture = new Texture2D(this.width, this.height);
        texture.filterMode = FilterMode.Point;
        previeRender.material.mainTexture = texture;
        prevBuild = !build;
        mesh = new Mesh();

        int width = this.width / 8;
        int height = this.height / 8;
        if (vertices == null || vertices.Length < (width + 1) * (height + 1)) {
            vertices = new Vector3[(width + 1) * (height + 1)];
            uvs = new Vector2[(width + 1) * (height + 1)];
            indices = new int[6 * (width) * (height)];
            int i = 0;
            for (int x = 0; x < width + 1; x++) {
                for (int y = 0; y < height + 1; y++) {
                    uvs[x + y * (width + 1)] = new Vector2(x / (float)width, y / (float)height);
                }
            }

            for (int x = 0; x < width; x++) {
                for (int y = 0; y < height; y++) {
                    indices[i++] = x + y * (width + 1);
                    indices[i++] = x + (y + 1) * (width + 1);
                    indices[i++] = x + 1 + (y + 1) * (width + 1);
                    
                    indices[i++] = x + y * (width + 1);
                    indices[i++] = x + 1 + (y + 1) * (width + 1);
                    indices[i++] = x + 1 + y * (width + 1);
                }
            }
        }
        
        
        for (int x = 0; x < this.width/8; x++) {
            for (int y = 0; y < this.height/8; y++) {
                //float v = equation(x * 10, y * 10);
                vertices[x + y * (this.width/8)] = new Vector3(x, 0, y);
            }
        }
        mesh.vertices = vertices;
        mesh.triangles = indices;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        meshRenderer.mesh = mesh;
    }

    void Update() {
        if (prevBuild != build) {
            prevBuild = build;
            IEnumerator enumerator = World.WorldMap.GenerateMap();
            while (enumerator.MoveNext()) {
                
            }
            Debug.Log(World.WorldMap.Map.geology.Length);

            
            noise = new Noise(seed);
            for (int x = 0; x < this.width/8 + 1; x++) {
                for (int y = 0; y < this.height/8 + 1; y++) {
                    vertices[x + y * (this.width/8 + 1)] = new Vector3(x * 8, 0, y * 8);
                }
            }
            mesh.vertices = vertices;
            mesh.triangles = indices;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            meshRenderer.mesh = mesh;
            
            StartCoroutine(ApplyEquationBlock());
        }
    }

    private Color[] temp;
    private void ApplyEquationYield(int px, int py, int w, int h) {
        if (temp == null || temp.Length != w * h) {
            temp = new Color[w * h];
        }
        for (int x = 0; x < w; x++) {
            for (int y = 0; y < h; y++) {
                Color c = reEasy(px + x, py + y);
                c.a = 1;
                temp[x + y * w] = c;  //voronoiEquation(x, y);
            }
        }
        
        texture.SetPixels(px, py, w, h, temp);
        texture.Apply();
    }

    private IEnumerator ApplyEquationBlock() {
        for (int x = 0; x < 8; x++) {
            for (int y = 0; y < 8; y++) {
                ApplyEquationYield(x * width / 8, y * height / 8, width / 8, height / 8);
                yield return null;
            }
        }
    }

    private void ApplyEquation() {
        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                colors[x + y * width] = reEasy(x, y);  //voronoiEquation(x, y);
            }
        }
        
        texture.SetPixels(colors);
        texture.Apply();
    }

    private Color getCellColor(Vector2 cell) {
        float cd = (float) noise.Wave2D(cell.x * biomeScale / mainScale, cell.y * biomeScale / mainScale);
        cd = cd * 0.5f + 0.5f;
        if (cd < cutOut) cd = 0;
        return new Color(cd, cd, cd);
        return new Color(
            noise.Random(cell.GetHashCode() + 2563),
            noise.Random(cell.GetHashCode() + 3198),
            noise.Random(cell.GetHashCode() + 2569));
    }
    
    private Color snow          = new Color(0.85f, 0.95f, 1f);
    private Color rock          = new Color(0.85f, 0.95f, 1f);
    private Color scorched      = new Color(0.85f, 0.95f, 1f);
    
    private Color iceland       = new Color(0.70f, 0.90f, 1.00f);
    private Color ocean         = new Color(0.07f, 0.14f, 0.62f);
    private Color sea           = new Color(0.14f, 0.25f, 1.00f);
    private Color beach         = new Color(0.98f, 1.00f, 0.40f);

    private Color swamp         = new Color(0f, 0.2f, 0.1f);
    private Color rain_forest   = new Color(0f, 0.46f, 0.05f);   // tropical rain forest | Amazonia
    private Color temp_forest   = new Color(0.09f, 0.62f, 0.2f); // temperate seasonal forest | Decidious
    private Color grassland     = new Color(0.29f, 1f, 0.2f);

    private Color taiga         = new Color(0.34f, 0.74f, 0.58f);    // boreal forest
    private Color tundra        = new Color(0.77f, 0.47f, 0.34f);
    
    private Color temp_desert   = new Color(0.71f, 0.57f, 0.35f);
    private Color desert        = new Color(1f, 0.99f, 0.64f);
    private Color savanna       = new Color(1f, 0.84f, 0.31f);
    private Color woodland      = new Color(0.8f, 0.67f, 0.25f);
    
    public Color getBiome(float height, float wet, float cold) {
        if (height < cutOut) {
            if (cold > 0.7) return iceland;
            if (height < cutOutBeach * 0.75f) return ocean;
            if (height < cutOutBeach) return sea;
            return beach;
        }

        // height = (height - cutOut) / (1 - cutOut);
        if (cold < 0.2) {
            if (wet < 0.3) return desert;
            if (wet < 0.4) return savanna;
            if (wet < 0.5) return woodland;
            if (wet < 0.9) return rain_forest;
            return swamp;
        }
        if (cold < 0.5) {
            if (wet < 0.2) return desert;
            if (wet < 0.3) return temp_desert;
            if (wet < 0.5) return grassland;
            if (wet < 0.9) return temp_forest;
            return swamp;
        }

        if (cold < 0.7) {
            if (wet < 0.1) return woodland;
            if (wet < 0.3) return grassland;
            if (wet < 0.7) return taiga;
            return temp_forest;
        }
        if (cold < 0.8) return taiga;
        if (cold < 0.9) return tundra;
        return snow;
    }
    
    /*public Color getBiome(float dry, float cold) {
        if (cold > 0.8) {
            if (dry < 0.12) return iceland;
        }
        if (dry < 0.05) return ocean;
        if (dry < 0.10) return sea;
        if (dry < 0.11) return beach;
  
        if (dry > 0.8) {
            if (cold < 0.1) return scorched;
            if (cold < 0.2) return rock;
            if (cold < 0.5) return tundra;
            return snow;
        }

        if (dry > 0.6) {
            if (cold < 0.25) return temp_desert;
            if (cold < 0.33) return savanna;
            if (cold < 0.66) return woodland;
            return taiga;
        }

        if (dry > 0.3) {
            if (cold < 0.16) return temp_desert;
            if (cold < 0.50) return grassland;
            if (cold < 0.83) return temp_forest;
            return rain_forest;
        }

        if (cold < 0.16) return desert;
        if (cold < 0.33) return grassland;
        if (cold < 0.36) return swamp;
        if (cold < 0.66) return temp_forest;
        return rain_forest;
    }*/
    private Color getColorRainbow(float c) {
        Color col = Color.black;
        if (c < 0.1f) col = Color.red;
        else if (c < 0.2f) col = Color.Lerp(Color.red, Color.yellow, 0.5f);
        else if (c < 0.3f) col = Color.yellow;
        else if (c < 0.4f) col = Color.green;
        else if (c < 0.5f) col = Color.Lerp(Color.green, Color.blue, 0.5f);
        else if (c < 0.6f) col = Color.blue;
        else if (c < 0.7f) col = Color.Lerp(Color.blue, Color.cyan, 0.5f);
        else if (c < 0.8f) col = Color.cyan;
        else if (c < 0.9f) col = Color.gray;
        else if (c <= 1.0f) col = Color.white;
        return col;
    }

    private Color reEasy(float x, float y) {
        
        float geo = World.WorldMap.Map.geology[(int) x + (int) y * width];
        float wet = World.WorldMap.Map.moisture[(int) x + (int) y * width];
        float tem = 1 - World.WorldMap.Map.temperature[(int) x + (int) y * width];
        Color cBiome = getBiome(geo, wet, tem);
        cBiome.a = geo;
        return new Color(cBiome.r * geo, cBiome.g * geo, cBiome.b * geo, 1);

        float f = 1;
        float px = x / (float) width;
        float py = y / (float) height;
        if (continentalType == ContinentalType.OVAL) {
            f = Mathf.Sqrt((x / (float) width - 0.5f) * (x / (float) width - 0.5f) +
                           (y / (float) height - 0.5f) * (y / (float) height - 0.5f)) * 2;
        } else if (continentalType == ContinentalType.SQUARE) {
            f = Mathf.Max(Mathf.Abs(px - 0.5f)) * 2;
        } else if (continentalType == ContinentalType.DOUBLE_SQUARE) {
            f = Mathf.Min(
                Mathf.Max(Mathf.Abs(px * 2 - 0.5f), Mathf.Abs(py - 0.5f)),
                Mathf.Max(Mathf.Abs(px * 2 - 1.5f), Mathf.Abs(py - 0.5f))) * 2f;
        } else if (continentalType == ContinentalType.DOUBLE_OVAL) {
            f = Mathf.Min(
                Vector2.Distance(new Vector2(px * 2, py), new Vector2(0.5F, 0.5F)),
                Vector2.Distance(new Vector2(px * 2, py), new Vector2(1.5F, 0.5F))) * 2f;
        }

        f = Interpolation.Exp5In(f);
        
        // Geology
        float o1 = (float) noise.Wave2D(x / mainScale, y / mainScale);
        float o2 = (float) noise.Wave2D(x / secondScale + 35.5f, y / secondScale + 37.9f);
        float o3 = (float) noise.Wave2D(x / thridScale + 75.6f, y / thridScale + 91.3f);
        float g = (o1 * mainPower + o2 * secondPower + o3 * thirdPower) / (mainPower + secondPower + thirdPower);
        float gH = (g * 0.5f + 0.5f);
        float gW = gH < cutOut? gH : (gH - cutOut) * (1 - islandsMin * 2f) + cutOut;
        
        // Islands / Montains
        float i1 = (float) noise.Wave2D(x / islandsScale + 96.5f, y / islandsScale + 132.98f);
        float i2 = (float) noise.Wave2D(x / (islandsScale / 2f) + 85.3f, y / (islandsScale / 2f) + 63.82f);
        float i = (i1 * 1f + i2 * 0.5f) / 1.5f;
        i = (i * 0.5f + 0.5f);

        // Ground Mask
        float groundMask = Mathf.Clamp01(gH - cutOutBeach) / (1 - cutOutBeach);
        groundMask = Interpolation.Exp10Out(Interpolation.Exp5Out(groundMask));
        
        // Sea Mask
        float seaMask = 1 - (Mathf.Clamp01(gH + (1 - cutOut)) - (1 - cutOut)) / cutOut;
        seaMask = Interpolation.Exp10Out(Interpolation.Exp5Out(seaMask));

        // Wet Mask
        float wetMask = 1 - (Mathf.Clamp01(gH - cutOut) / (1 - cutOut));
        wetMask = wetMask;
        
        // Island Earth
        float iE = Interpolation.Smooth(i);
        
        // Island Sea
        float iI = Interpolation.Circle(Interpolation.Exp5In(i));
        
        // Final Height
        float h = gW
                  + (iE * groundMask * islandsMin)
                  + (seaMask * iI * (1 - cutOut));
        h = Mathf.Lerp(h, cutOutBeach / 2f, f);
        
        // Latitude
        float lat = Mathf.Abs(y / height - 0.5f) * 2.0f;
        
        // Cold
        float lCold = Interpolation.Pow2OutInverse(lat) * 1.15f;
        float hCold = (h < cutOut ? (1 - h / cutOut) * 0.5f : (h - cutOut) / (1 - cutOut)) * 0.75f;
        
        // Final Cold
        float c = Mathf.Clamp01(hCold + lCold); // COLD

        // Wet
        float m1 = (float) noise.Wave2D(x / biomeNoiseScale + 15.7f, y / biomeNoiseScale + 963.78f);
        float m2 = (float) noise.Wave2D(x / (biomeNoiseScale / 4f) + 85.32f, y / (biomeNoiseScale / 4f) + 3.46f);
        float m = (m1 + m2 * 0.25f) / 1.25f;
        m = Interpolation.Smooth(m * 0.5f + 0.5f);
        float w = h < cutOutBeach ? 1 : Mathf.Lerp(m, wetMask, 0.05f); // WET

        Color colorBiome = getBiome(h, w, c);
        colorBiome.a = h;
        return getColorRainbow(groundMask);// new Color(colorBiome.r * h, colorBiome.g * h, colorBiome.b * h, 1);;
        // return getColorRainbow(h < cutOut ? 1 - h / cutOut: (h - cutOut) / (1 - cutOut));//
    }

    private Color mix(Color colorA, float wA, Color colorB, float wB) {
        return (colorA * wA + colorB * wB) / (wA + wB);
    }

    private Color mix(Color colorA, float wA, Color colorB, float wB, Color colorC, float wC) {
        return (colorA * wA + colorB * wB + colorC * wC) / (wA + wB + wC);
    }

    /*private Color[][] biomes = { 
        new [] {
            new Color(0.60f, 0.73f, 0.66f), 
            new Color(0.60f, 0.73f, 0.66f), 
            new Color(0.66f, 0.78f, 0.64f), 
            new Color(0.66f, 0.78f, 0.64f), 
            new Color(0.76f, 0.82f, 0.66f), 
            new Color(0.91f, 0.86f, 0.77f),
        },
        new [] {
            new Color(0.60f, 0.73f, 0.66f), 
            new Color(0.66f, 0.78f, 0.64f), 
            new Color(0.66f, 0.78f, 0.64f), 
            new Color(0.76f, 0.82f, 0.66f), 
            new Color(0.76f, 0.82f, 0.66f), 
            new Color(0.89f, 0.90f, 0.78f),
        },
        new [] {
            new Color(0.99f, 0.99f, 0.99f), 
            new Color(0.99f, 0.99f, 0.99f), 
            new Color(0.99f, 0.99f, 0.99f), 
            new Color(0.60f, 0.73f, 0.66f), 
            new Color(0.73f, 0.73f, 0.73f), 
            new Color(0.6f, 0.6f, 0.6f),
        },
    };*/
    private class BiomeSlot {
        public Color color;
        public float equator0;
        public float equator1;
        public float high;
        public float middle;
        public float low; // current * (lerp(equator0, equator1, equator))

        public BiomeSlot(Color color, float equator0, float equator1,  float low, float middle,float high) {
            this.color = color;
            this.equator0 = equator0;
            this.equator1 = equator1;
            this.high = high;
            this.middle = middle;
            this.low = low;
        }
    }

    private BiomeSlot[] bSlots = {
        new BiomeSlot(new Color(1f, 0.98f, 0.85f),    0.0f, 0.1f, 5, 5, 0),
        new BiomeSlot(new Color(0.60f, 0.73f, 0.66f), 0.0f, 0.1f, 2, 2, 0),
        
        new BiomeSlot(new Color(0.60f, 0.73f, 0.66f), 0.1f, 0.5f, 2, 0, 0),
        new BiomeSlot(new Color(0.66f, 0.78f, 0.64f), 0.1f, 0.5f, 1, 2, 0),
        new BiomeSlot(new Color(0.76f, 0.82f, 0.66f), 0.1f, 0.5f, 0, 2, 0),
        
        new BiomeSlot(new Color(0.76f, 0.82f, 0.66f), 0.5f, 1.0f, 2, 2, 0),
        
        new BiomeSlot(new Color(0.99f, 0.99f, 0.99f), 0.0f, 1.0f, 0, 0, 3),
        new BiomeSlot(new Color(0.51f, 0.51f, 0.52f), 0.0f, 1.0f, 0, 0, 3),
        
        new BiomeSlot(new Color(0.99f, 0.99f, 0.99f), 0.7f, 1.0f, 5, 5, 5),
    };

    private float fastPow(float a) {
        float b = a * a * a * a;
        return b * b;
    }

    private Vector2 getCellInt(Vector2Int cell, float fractal) {
        var t = 2 * Mathf.PI * noise.Random(Noise.SingleToInt32Bits(cell.x * 1529.164f + cell.y * 1563.325f));
        var vec = new Vector2(0.5f + fractal * Mathf.Cos(t), 0.5f + fractal * Mathf.Sin(t));
        return cell + vec;
    }
    
    private Vector2 getFCell(Vector2 cell, float fractal) {
        float wave = (float) noise.Wave2D(cell.x * 10, cell.y * 10);
        return new Vector2(cell.x + wave * fractal, cell.y + wave * fractal);
    }
    
    private Vector2 getFractalCell(Vector2 cell, float fractal) {
        float rand = noise.Random(Noise.SingleToInt32Bits((float) noise.Wave2D(cell.x * 0.12684f, cell.y * 0.68416f))) * Mathf.PI * 2;
        return new Vector2(cell.x + Mathf.Cos(rand) * fractal, cell.y + Mathf.Sin(rand) * fractal);
    }

    private int voronoiDistance(float x, float y, float fractal, Vector2[] cells, float[] distance) {
        Vector2Int p = new Vector2Int(Mathf.FloorToInt(x), Mathf.FloorToInt(y));
        Vector2 f = new Vector2(x, y);
        
        Vector2 mr = new Vector2();
        int center = 4;
        float res = 9.0f;
        for (int j = -1; j <= 1; j++)
        for (int i = -1; i <= 1; i++) {
            Vector2Int b = new Vector2Int(i, j);
            Vector2 r = getCellInt(p + b, fractal) - f;
            float d = Vector3.Dot(r, r);

            if (d < res) {
                res = d;
                mr = r;
                center = (j + 1) * 3 + i + 1;
            }
        }
        
        int n = 0;
        for (int j = -1; j <= 1; j++)
        for (int i = -1; i <= 1; i++) {
            Vector2Int b = new Vector2Int(i, j);
            Vector2 c = getCellInt(p + b, fractal);
            Vector2 r = c - f;
            float d = Vector3.Dot(0.5f * (mr + r), Vector3.Normalize(r - mr));
            cells[n] = c;
            distance[n] = d;
            n++;
        }

        return center;
    }

    private void getVoronoi(float x, float y, float fractal, Vector2[] cells, float[] distance) {
        Vector2 p = new Vector2(Mathf.Floor(x), Mathf.Floor(y));
        Vector2 f = new Vector2(x, y);

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
                    cells[0] = cell;
                } else if (d < distance[1]) {
                    distance[2] = distance[1];
                    distance[1] = d;
                    cells[2] = cells[1];
                    cells[1] = cell;
                } else if (d < distance[2]) {
                    distance[2] = d;
                    cells[2] = cell;
                }
            }
        }

        /*distance[0] = Mathf.Sqrt(distance[0]);
        distance[1] = Mathf.Sqrt(distance[1]);
        distance[2] = Mathf.Sqrt(distance[2]);*/
    }

    public float equation(int x, int y) {
        float i = (float) noise.Wave2D(
            x / islandsScale + noise.RandomRange(10000, 0, width),
            y / islandsScale + noise.RandomRange(10001, 0, height));
        float i2 = (float) noise.Wave2D(
            x / islandsScale * 4 + noise.RandomRange(50000, 0, width),
            y / islandsScale * 4 + noise.RandomRange(50001, 0, height));
        i = i + i2 * 0.25f;
        i = Mathf.Clamp01(i - islandsMin) * (1 / (1 - islandsMin));
        i = (i * 2 - 1);
        float underZero = Mathf.Clamp(i - islandsMin, -1 - islandsMin, 0) / (islandsMin + 1);
        float overZero = Mathf.Clamp(i - islandsMin, 0, 1) / (1 - islandsMin);
        i = underZero + overZero * 0.75f;

        float a = (float) noise.Wave2D(
            x / mainScale + noise.RandomRange(20000, 0, width),
            y / mainScale + noise.RandomRange(20001, 0, height));

        float b = (float) noise.Wave2D(
            x / secondScale + noise.RandomRange(30000, 0, width),
            y / secondScale + noise.RandomRange(30001, 0, height));

        float c = (float) noise.Wave2D(
            x / thridScale + noise.RandomRange(40000, 0, width),
            y / thridScale + noise.RandomRange(40001, 0, height));

        float px = x / (float) width;
        float py = y / (float) height;

        float d = 0;
        float f = 1;

        if (continentalType == ContinentalType.OVAL) {
            d = Mathf.Sqrt((x / (float) width - 0.5f) * (x / (float) width - 0.5f) +
                           (y / (float) height - 0.5f) * (y / (float) height - 0.5f)) * 2;
        } else if (continentalType == ContinentalType.SQUARE) {
            d = Mathf.Max(Mathf.Abs(px - 0.5f), Mathf.Abs(py - 0.5f)) * 2;
        } else if (continentalType == ContinentalType.DOUBLE_SQUARE) {
            d = Mathf.Min(
                Mathf.Max(Mathf.Abs(px * 2 - 0.5f), Mathf.Abs(py - 0.5f)),
                Mathf.Max(Mathf.Abs(px * 2 - 1.5f), Mathf.Abs(py - 0.5f))) * 2f;
        } else if (continentalType == ContinentalType.DOUBLE_OVAL) {
            d = Mathf.Min(
                Vector2.Distance(new Vector2(px * 2, py), new Vector2(0.5F, 0.5F)),
                Vector2.Distance(new Vector2(px * 2, py), new Vector2(1.5F, 0.5F))) * 2f;
        }

        float complex = Mathf.Clamp01(1 - (Mathf.Clamp01(d - smoothContinental) / (1 - smoothContinental)));
        f = (Mathf.Max(a, i) * mainPower + b * secondPower + c * thirdPower) / (mainPower + secondPower + thirdPower);
        f = f * 0.5f + 0.5f;
        f = (f * complex) + (cutOut * 0.5f) * (1 - complex);
        
        return f;
    }

    private float smin(float a, float b, float k) {
        float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
        return Mathf.Lerp(b, a, h) - k * h * (1.0f - h);
    }
}
