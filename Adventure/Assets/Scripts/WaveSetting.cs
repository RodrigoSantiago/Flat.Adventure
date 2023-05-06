using System;
using Adventure.Data;
using Adventure.Logic;
using UnityEngine;

[Serializable]
public class WaveSetting {
    public float scale;
    public int octaves;
    public float lacunarity;
    public float persistence;
    [HideInInspector] public float[] offsetX;
    [HideInInspector] public float[] offsetY;

    public void buildCoords(Noise noise) {
        offsetX = new float[octaves];
        offsetY = new float[octaves];
        for (int i = 0; i < octaves; i++) {
            offsetX[i] = noise.NextRandom() * 200 - 100;
            offsetY[i] = noise.NextRandom() * 200 - 100;
        }
    }
}