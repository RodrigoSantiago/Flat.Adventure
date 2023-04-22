using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoxelPreview : MonoBehaviour {

    public MapPreview map;
    public GameObject prefabChunk;
    private List<ChunkPreview> chunks = new List<ChunkPreview>();
    
    public int x, y;
    public int w, h, d;
    public int size = 16;
    public int level = 1;

    private float changeT = 0;
    private Coroutine last;
    private bool colorOnly;
    
    void Start() {
        
    }

    void Update() {
        if (changeT > 0) {
            changeT -= Time.deltaTime;
            if (changeT < 0) {
                last = StartCoroutine(Remesh(colorOnly));
                colorOnly = false;
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

    public void Refresh(bool colorOnly) {
        this.colorOnly = colorOnly;
        OnValidate();
    }

    public IEnumerator Remesh(bool colorOnly) {
        int chunkCountX = w / (size * level);
        int chunkCountY = h / (size * level);
        
        while (chunks.Count < chunkCountX * chunkCountY) {
            chunks.Add(Instantiate(prefabChunk).GetComponent<ChunkPreview>());
        }

        float t = Time.realtimeSinceStartup;
        int i = 0;
        for (int x = 0; x < chunkCountX; x++) {
            for (int y = 0; y < chunkCountY; y++) {
                ChunkPreview c = chunks[i++];
                c.x = this.x + (x * size * level);
                c.y = this.y + (y * size * level);
                c.d = d;
                c.size = size;
                c.level = level;
                c.map = map;
                c.gameObject.transform.position = transform.position + (new Vector3(x * level, 0, y * level) * 0.1f);
                c.gameObject.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

                c.Remesh(colorOnly);
                if (Time.realtimeSinceStartup - t > 0.016) {
                    yield return null;
                    t = Time.realtimeSinceStartup;
                }
            }
        }
    }
}
