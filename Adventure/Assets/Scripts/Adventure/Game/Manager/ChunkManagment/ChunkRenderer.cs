using System;
using System.Collections.Generic;
using Adventure.Logic;
using UnityEngine;

namespace Adventure.Game.Manager.ChunkManagment {
    public class ChunkRenderer {

        private ChunkHolder[] renderBlock = new ChunkHolder[8];

        private int _maxLod;
        
        public bool renderSplit;
        public int maxLod { get => _maxLod; set => _maxLod = Math.Min(value, chunks.Length); }

        private WorldSettings settings;
        private Dictionary<Vector3Int, ChunkHolder>[] chunks;
        private RenderParams rParams;
        private ChunkLodBox[] lodBox;

        private static readonly Vector3Int[] vertexLoop = {
            new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0),
            new Vector3Int(0, 1, 0), new Vector3Int(1, 1, 0),
            new Vector3Int(0, 0, 1), new Vector3Int(1, 0, 1),
            new Vector3Int(0, 1, 1), new Vector3Int(1, 1, 1),
        };
        
        public ChunkRenderer(WorldSettings settings, Dictionary<Vector3Int, ChunkHolder>[] chunks, Material material, int maxLod = 4) {
            this.settings = settings;
            this.chunks = chunks;
            this.maxLod = maxLod;
            this.rParams = new RenderParams(material);
            this.lodBox = new ChunkLodBox[chunks.Length];
        }

        public void UpdateRenderBox(int lod, ChunkLodBox lodBox) {
            if (lod < 0 || lod >= this.lodBox.Length) return;

            this.lodBox[lod] = lodBox;
        }

        public void Render() {
            for (int i = 0; i < maxLod; i++) {
                RenderChunks(i);
            }
        }

        private bool ReadBlock(int lod, Vector3Int hPos) {
            int lodMul = 1 << lod;
            int n = 16 * lodMul;
            
            bool aligned = true;
            bool failToRender = false;
            for (int i = 0; i < 8; i++) {
                var pos = hPos + vertexLoop[i] * n;
                if (lod > 0 && lodBox[lod - 1].Contains(pos)) {
                    aligned = false;
                    renderBlock[i] = null;
                    continue;
                }
                var exists = chunks[lod].TryGetValue(pos, out renderBlock[i]);
                if (!exists || 
                    (renderBlock[i].chunk == null) ||
                    (!renderBlock[i].chunk.IsSingle() && !renderBlock[i].ready)) {
                    failToRender = true;
                }
            }

            return failToRender && aligned;
        }

        private void RenderChunk(ChunkHolder holder, Matrix4x4 scale) {
            var matrix = Matrix4x4.Translate(holder.local) * scale;
            Graphics.RenderMesh(in rParams, holder.mesh, 0, matrix);
        }

        private void RenderChunk(ChunkHolder holder, Matrix4x4 scale, ChunkLodBox lodBox) {
            var matrix = Matrix4x4.Translate(holder.local) * scale;
            if (holder.lodMesh != null && lodBox.LodLimit(holder.local, out _, out _)) {
                Graphics.RenderMesh(in rParams, holder.lodMesh, 0, matrix);
            }
            Graphics.RenderMesh(in rParams, holder.mesh, 0, matrix);
        }
        
        private void RenderChunks(int lod) {
            int lodMul = 1 << lod;
            int lodMul1 = 1 << (lod + 1);
            int lodMul0 = 1 << (lod > 0 ? lod - 1 : 0);
            int n = 16 * lodMul;
            int n1 = 16 * lodMul1;
            int n0 = 16 * lodMul0;
            var matScale = Matrix4x4.Scale(Vector3.one * (lodMul * (renderSplit ? 0.95f : 1)));
            var matScale1 = Matrix4x4.Scale(Vector3.one * (lodMul1 * (renderSplit ? 0.95f : 1)));
            var matScale0 = Matrix4x4.Scale(Vector3.one * (lodMul0 * (renderSplit ? 0.95f : 1)));
            
            int yMin = Math.Max(0, lodBox[lod].min.y);
            int yMax = Math.Min(settings.height, lodBox[lod].max.y);

            for (int y = yMin; y < yMax; y += n + n)
            for (int x = lodBox[lod].min.x; x < lodBox[lod].max.x; x += n + n)
            for (int z = lodBox[lod].min.z; z < lodBox[lod].max.z; z += n + n) {
                var hPos = settings.Pos(new Vector3Int(x, y, z));

                bool tryRenderHigher = ReadBlock(lod, hPos) && lod + 1 < maxLod;

                if (tryRenderHigher && chunks[lod + 1].TryGetValue(hPos, out var high) && high.mesh != null) {
                    RenderChunk(high, matScale1);
                    continue;
                }
                
                for (int i = 0; i < 8; i++) {
                    var pos = settings.Pos(hPos + vertexLoop[i] * n);
                    if (lod > 0 && lodBox[lod - 1].Contains(pos)) continue;
                    
                    var holder = renderBlock[i];
                    if (holder != null && holder.mesh != null) {
                        RenderChunk(holder, matScale, lodBox[lod]);
                        
                    } else if (lod > 0 && (holder == null || holder.chunk == null || 
                                           (!holder.chunk.IsSingle() && holder.mesh == null))) {
                        
                        for (int j = 0; j < 8; j++) {
                            var pos0 = settings.Pos(pos + vertexLoop[j] * n0);
                            if (!chunks[lod - 1].TryGetValue(pos0, out var low) || low.mesh == null) continue;

                            RenderChunk(low, matScale0);
                        }
                    }
                }
            }
        }
    }
}