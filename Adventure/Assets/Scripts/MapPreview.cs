using System;
using System.Collections;
using System.Collections.Generic;
using Adventure.Data;
using Adventure.Logic;
using UnityEngine;

public class MapPreview : MonoBehaviour {

    public enum ViewColor {
        BLACK_WHITE, GRAY, LOW_GRAY, HIGH_GRAY, RAINBOW, SPLIT_RAINBOW, BIOMES, GROUND_SEA
    }
    public enum ViewType {
        GEO, SPLIT, OVER, UNDER, GROUND_MASK, SEA_MASK, MOUNTAIN, MOUNTAIN_SLICE, MOUNTAIN_HILL, MOUNTAIN_NOISE, MOUNTAIN_FINAL,
        PLAINS, GROUND_SEA
    }
    
    public Transform preview;
    public Renderer previewRender;
    
    [Header("Size Preview")]
    public int width = 2048;
    public int height = 1024;
    [Range(1, 10)]
    public int level = 8;
    public long seed = 123456789;
    
    [Header("View Shift")]
    public ViewColor viewColor = ViewColor.RAINBOW;
    public ViewType viewType = ViewType.GEO;
    private ViewColor viewColorOld = ViewColor.RAINBOW;

    [Header("Main Geology")] 
    public WaveSetting mainNoise;
    [Range(0, 1)] public float seaLevel = 0.5f;
    [Range(0, 1)] public float seaHeight = 0.25f;
    
    [Header("Mountain")]
    public WaveSetting mountains;
    public WaveSetting mountainsSlice;
    public WaveSetting mountainsNoise;
    [Range(0, 1)] public float mountainMin = 0.1f;
    [Range(0, 1)] public float mountainMax = 0.9f;
    [Range(0, 1)] public float mountainNoiseInfluence = 0.1f;
    
    [Header("Plains")]
    public WaveSetting plains;
    [Range(0, 1)] public float plainsHeight = 0.3f;

    [Header("River")]
    public int riverOctaves = 1;
    public float riverScale = 50;
    public float riverLac = 2.0f;
    public float riverPer = 0.5f;
    public float riverSize = 0.1f;
    public float riverDeep = 0.1f;

    [Header("Heat")]
    public int heatOctaves = 1;
    public float heatScale = 200;
    public float heatLac = 2.0f;
    public float heatPer = 0.5f;
    public float heatLatInfluence = 0.5f;

    [Header("Moisture")]
    public int moistureOctaves = 2;
    public float moistureScale = 200;
    public float moistureLac = 2.0f;
    public float moisturePer = 0.5f;
    
    private Coroutine last;
    private float changeT;
    private Texture2D texture;
    private readonly Color[] colors = new Color[17 * 17];
    private int vWidth;
    private int vHeight;

    void Start() {
        changeT = 0.5f;
    }

    void Update() {
        if (changeT > 0) {
            changeT -= Time.deltaTime;
            if (changeT < 0) {
                last = StartCoroutine(BuildMap(viewColorOld != viewColor) );
                viewColorOld = viewColor;
            }
        }
    }

    private void OnValidate() {
        if (!Application.isPlaying) return;
        
        if (last != null) {
            StopCoroutine(last);
        }
        changeT = 1;
    }

    private IEnumerator BuildMap(bool colorOnly) {
        int iLevel = 1;
        for (int i = 1; (i < level || width / iLevel > 2048) && (width / (iLevel << 1) > 0); i++) {
            iLevel = iLevel << 1;
        }

        vWidth = width / iLevel;
        vHeight = height / iLevel;
        if (texture == null || texture.width != vWidth || texture.height != vHeight) {
            texture = new Texture2D(vWidth, vHeight, TextureFormat.RGB24, false);
            texture.filterMode = FilterMode.Point;
            
            preview.localScale = new Vector3(10, height/(float)width * 10f, 1);
            previewRender.material.mainTexture = texture;
        }

        Init();

        foreach (var prev in FindObjectsOfType<VoxelPreview>()) {
            prev.Refresh(colorOnly);
        }
        float t = Time.realtimeSinceStartup;
        for (int x = 0; x < vWidth; x+= 16) {
            for (int y = 0; y < vHeight; y+= 16) {
                BuildBlock(x, y, Math.Min(16, vWidth - x), Math.Min(16, vHeight - y));
                texture.SetPixels(x, y, Math.Min(16, vWidth - x), Math.Min(16, vHeight - y), colors);
                
                if (Time.realtimeSinceStartup - t > 0.016) {
                    texture.Apply();
                    yield return null;
                    t = Time.realtimeSinceStartup;
                }
            }
        }
        texture.Apply();
    }

    // sample points jumping from level
    private void BuildBlock(int x, int y, int w, int h) {
        int iLevel = 1;
        for (int i = 1; (i < level || width / iLevel > 2048) && (width / (iLevel << 1) > 0); i++) {
            iLevel = iLevel << 1;
        }
        for (int i = x; i < x + w && i < vWidth; i++) {
            for (int j = y; j < y + h && j < vHeight; j++) {
                colors[(i - x) + (j - y) * w] = getColor(i * iLevel, j * iLevel);
            }
        }
    }
    
    // -- Valores internos
    private Noise noise;
    private float gLevel;
    private float[] _mainOctavePos;
    private float[] _heatOctavePos;
    private float[] _riverOctavePos;
    private float[] _river2OctavePos;
    private float[] _mountainOctavePos;
    private float[] _mountainPlanOctavePos;
    private float[] _moistureOctavePos;
    private void Init() {
        noise = new Noise(seed);
        gLevel = 1 - seaLevel;

        noise.RandomReset(1698415);
        mainNoise.buildCoords(noise);
        mountains.buildCoords(noise);
        mountainsSlice.buildCoords(noise);
        mountainsNoise.buildCoords(noise);
        plains.buildCoords(noise);

        _heatOctavePos = new float[heatOctaves * 2];
        for (int i = 0; i < heatOctaves * 2; i++) {
            _heatOctavePos[i] = noise.NextRandom() * 200 - 100;
        }
        _riverOctavePos = new float[riverOctaves * 2];
        for (int i = 0; i < riverOctaves * 2; i++) {
            _riverOctavePos[i] = noise.NextRandom() * 200 - 100;
        }
        _river2OctavePos = new float[riverOctaves * 2];
        for (int i = 0; i < riverOctaves * 2; i++) {
            _river2OctavePos[i] = noise.NextRandom() * 200 - 100;
        }
        _moistureOctavePos = new float[moistureOctaves * 2];
        for (int i = 0; i < moistureOctaves * 2; i++) {
            _moistureOctavePos[i] = noise.NextRandom() * 200 - 100;
        }
    }

    private float smoothShift(float t, float center) {
        float n = (t - center);
        float max = center > 0.5f ? center : 1 - center;
        float p = Math.Abs(n > 0 ? n / (1 - center) : n / center);
        float n2 = t > center ? ((t - center) / (1 - center)) * 0.5f + 0.5f : t / center * 0.5f;
        n = (n / max) * 0.5f + 0.5f;
        return n * (1 - p) + n2 * p;
    }

    private Color getGeoOnlyBiomeSplit(float v, float sea) {
        if (v < sea) {
            v = (v / sea) * 0.5f;
        } else {
            v = (v - sea) / (1 - sea) * 0.5f + 0.5f;
        }
        if (v < 0.4) return new Color(0.01f, 0.09f, 0.85f);
        if (v < 0.5) return new Color(0f, 0.35f, 1f);
        if (v < 0.52) return new Color(0.97f, 0.94f, 0.54f);
        if (v < 0.7) return new Color(0.16f, 0.95f, 0.31f);
        if (v < 0.8) return new Color(0, 0.75f, 0);
        if (v < 0.9) return new Color(0.5f, 0.45f, 0.42f);
        return new Color(0.89f, 0.98f, 1f);
    }

    private Color getGeoOnlyBiome(float v) {
        if (v < 0.4) return new Color(0.01f, 0.09f, 0.85f);
        if (v < 0.5) return new Color(0f, 0.35f, 1f);
        if (v < 0.52) return new Color(0.97f, 0.94f, 0.54f);
        if (v < 0.7) return new Color(0.16f, 0.95f, 0.31f);
        if (v < 0.8) return new Color(0, 0.75f, 0);
        if (v < 0.9) return new Color(0.5f, 0.45f, 0.42f);
        return new Color(0.89f, 0.98f, 1f);
    }

    private Color getPreviewColor(float v) {
        if (viewColor == ViewColor.BLACK_WHITE) {
            return v < 0.5 ? Color.black : Color.white;
        } else if (viewColor == ViewColor.GROUND_SEA) {
            return v < seaHeight ? Color.blue : Color.green;
        } else if (viewColor == ViewColor.GRAY) {
            return new Color(v, v, v, 0);
        } else if (viewColor == ViewColor.LOW_GRAY) {
            v = v < 0.5 ? v * 2 : 0;
            return new Color(v, v, v, 0);
        } else if (viewColor == ViewColor.HIGH_GRAY) {
            v = v >= 0.5 ? 1-((v - 0.5f) * 2) : 0;
            return new Color(v, v, v, 0);
        } else if (viewColor == ViewColor.RAINBOW) {
            return getRainbown(v);
        } else if (viewColor == ViewColor.SPLIT_RAINBOW) {
            return v < 0.5 ? getRainbown(v * 2) : getRainbown((v - 0.5f) * 2);
        } else if (viewColor == ViewColor.BIOMES) {
            return getGeoOnlyBiomeSplit(v, seaHeight);
        }
        return new Color(v, v, v, 0);
    }

    private Color getRainbown(float v) {
        Color col = Color.black;
        if (v < -0.00001f) col = Color.magenta;
        else if (v < 0.0001f) col = Color.white;
        else if (v < 0.1f) col = new Color(0.94f, 0.94f, 0.94f);
        else if (v < 0.2f) col = Color.gray;
        else if (v < 0.3f) col = Color.cyan;
        else if (v < 0.4f) col = Color.Lerp(Color.blue, Color.cyan, 0.5f);
        else if (v < 0.5f) col = Color.blue;
        else if (v < 0.6f) col = Color.Lerp(Color.blue, Color.green, 0.5f);
        else if (v < 0.7f) col = Color.green;
        else if (v < 0.8f) col = Color.yellow;
        else if (v < 0.9f) col = Color.Lerp(Color.red, Color.yellow, 0.5f);
        else if (v < 0.9999f) col = Color.red;
        else if (v <= 1f) col = new Color(0.84f, 0f, 0f);
        return col;
    }
    
    // X e Y varies from 0 to 'real width' and 'real height'
    public Color getColor(float x, float y) {
        float geoBase = WaveNoise(mainNoise, x, y);
        float geoSplit = smoothShift(geoBase, seaLevel);
        float hOver = Mathf.Clamp01(geoSplit - 0.5f) * 2;
        float hUnder = 1 - Mathf.Clamp01(geoSplit * 2f);
        
        float seaMask = Interpolation.Exp10Out(hUnder);
        float groundMask = Interpolation.Exp10Out(hOver);
        
        float lat = Math.Abs(y / height - 0.5f) * 2f;
        float mountains = WaveNoise(this.mountains, x, y);
        float mountainsSlice = WaveNoise(this.mountainsSlice, x, y, false);
        mountainsSlice = 1-Mathf.Abs(mountainsSlice);
        float mountainsHill = mountains * mountainsSlice;
        float mountainNoiseInfluence = this.mountainNoiseInfluence * (mountainsHill * mountainsHill);
        mountainsHill = Mathf.Clamp01((mountainsHill - mountainMin) / (mountainMax - mountainMin));
        float mountainsNoise = WaveNoise(this.mountainsNoise, x, y, false);
        
        float plains = WaveNoise(this.plains, x, y) * plainsHeight;
        mountainsHill = Math.Max(mountainsHill, plains);
        
        float mountainsFinal = ((mountainsHill * 2f - 1f) * (1 - mountainNoiseInfluence)) + (mountainsNoise * mountainNoiseInfluence);
        mountainsFinal = mountainsFinal * 0.5f + 0.5f;

        float groundMountains = mountainsFinal * groundMask;
        float groundSea = groundMountains * (1 - seaHeight) + seaHeight - seaMask * seaHeight;
        float finalH =
            viewType == ViewType.GROUND_SEA ? groundSea :
            viewType == ViewType.MOUNTAIN_FINAL ? groundMountains :
            viewType == ViewType.PLAINS ? plains :
            viewType == ViewType.MOUNTAIN_NOISE ? mountainsNoise :
            viewType == ViewType.MOUNTAIN_HILL ? mountainsHill :
            viewType == ViewType.MOUNTAIN_SLICE ? mountainsSlice :
            viewType == ViewType.MOUNTAIN ? mountains :
            viewType == ViewType.GROUND_MASK ? groundMask :
            viewType == ViewType.SEA_MASK ? seaMask :
            viewType == ViewType.UNDER ? hUnder :
            viewType == ViewType.OVER ? hOver :
            viewType == ViewType.GEO ? geoBase : geoSplit;
        Color col = getPreviewColor(finalH);
        col.a = finalH;
        return col;
    }

    private float WaveNoise(WaveSetting wave, float x, float y, bool normalize = true, Interpolation.Interpolator interpolator = null) {
        float sum = 0;
        float a = 1, f = 1, geoBase = 0;
        for (int i = 0; i < wave.octaves; i++) {
            geoBase += (float) noise.Wave2D(
                x / wave.scale * f + (wave.offsetX?[i] ?? 0), 
                y / wave.scale * f + (wave.offsetY?[i] ?? 0)) * a;
            if (i == 0 && interpolator != null) {
                geoBase = (interpolator.Invoke(geoBase * 0.5f + 0.5f) - 0.5f) * 2f;
            }
            sum += a;
            a *= wave.persistence;
            f *= wave.lacunarity;
        }
        geoBase /= sum;
        if (normalize) {
            geoBase = geoBase * 0.5f + 0.5f;
        }

        return geoBase;
    }

    private float MainPerlim(int octaves, float scale, float per, float lac, float[] offset, float x, float y, 
        bool normalize = true, Interpolation.Interpolator interpolator = null, int i = 0, float geoBase = 0) {
        float sum = 0;
        float a = 1, f = 1;
        for (; i < octaves; i++) {
            geoBase += (float) noise.Wave2D(
                x / scale * f + (offset?[i * 2] ?? 0), 
                y / scale * f + (offset?[i * 2 + 1] ?? 0)) * a;
            if (i == 0 && interpolator != null) {
                geoBase = (interpolator.Invoke(geoBase * 0.5f + 0.5f) - 0.5f) * 2f;
            }
            sum += a;
            a *= per;
            f *= lac;
        }
        geoBase /= sum;
        if (normalize) {
            geoBase = geoBase * 0.5f + 0.5f;
        }

        return geoBase;
    }

    private float MainPerlimPoint(float start, float inLac, int octaves, float scale, float per, float lac, float[] offset, float x, float y, bool normalize = true) {
        float sum = 1;
        float a = inLac, f = 1;
        for (int i = 0; i < octaves; i++) {
            start += (float) noise.Wave2D(
                x / scale * f + (offset?[i * 2] ?? 0), 
                y / scale * f + (offset?[i * 2 + 1] ?? 0)) * a;
            sum += a;
            a *= per;
            f *= lac;
        }
        start /= sum;
        if (normalize) {
            start = start * 0.5f + 0.5f;
        }

        return start;
    }
    
}
