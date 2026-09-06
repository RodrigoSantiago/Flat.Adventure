using System;
using Game.Data;
using Game.Worlds.Rendering;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Worlds {
    public class WorldRender {
        private readonly WorldManager worldManager;

        private readonly Plane[] frustumPlanes = new Plane[6];
        private float cachedShadowDistance;

        private struct RenderCommand {
            public ChunkRenderData chunk;
            public IndexPos pos;
            public int lod;
            public RenderVisibility visibility;
        }

        private readonly List<RenderCommand> pendingCommands = new ();
        private readonly HashSet<IndexPos> activeFallbackParentPositions = new ();

        private readonly ChunkRenderData[] subChunks = new ChunkRenderData[8];
        private readonly IndexPos[] subPositions = new IndexPos[8];
        
        private readonly RenderParams visibleRenderParams;
        private readonly RenderParams shadowOnlyRenderParams;

        public WorldRender(WorldManager worldManager) {
            this.worldManager = worldManager;
            
            visibleRenderParams = new RenderParams(GameManager.Instance.MeshGenerator.GroundMaterial) {
                layer = 0,
                receiveShadows = true,
                shadowCastingMode = ShadowCastingMode.On
            };

            shadowOnlyRenderParams = new RenderParams(GameManager.Instance.MeshGenerator.DummyMaterial) {
                layer = 0,
                receiveShadows = false,
                shadowCastingMode = ShadowCastingMode.ShadowsOnly
            };
        }

        public Camera GetActiveCamera() {
            return GameManager.Instance.PlayerCamera;
        }

        public void RenderFrame() {
            Camera cam = GetActiveCamera();
            if (cam == null) return;

            GeometryUtility.CalculateFrustumPlanes(cam, frustumPlanes);
            cachedShadowDistance = QualitySettings.shadowDistance;
            
            pendingCommands.Clear();
            activeFallbackParentPositions.Clear();

            var lightDir = RenderSettings.sun != null ? RenderSettings.sun.transform.forward : Vector3.down;

            CollectLodRegion(
                lod: 0,
                viewLod: worldManager.viewLod0,
                chunkSize: 32,
                size: 8,
                excludeViewLod: null,
                excludeSize: 0,
                excludeChunkSize: 0,
                lightDir: lightDir
            );

            CollectLodRegion(
                lod: 1,
                viewLod: worldManager.viewLod1,
                chunkSize: 64,
                size: 12,
                excludeViewLod: worldManager.viewLod0,
                excludeSize: 8,
                excludeChunkSize: 32,
                lightDir: lightDir
            );

            CollectLodRegion(
                lod: 2,
                viewLod: worldManager.viewLod2,
                chunkSize: 128,
                size: 16,
                excludeViewLod: worldManager.viewLod1,
                excludeSize: 12,
                excludeChunkSize: 64,
                lightDir: lightDir
            );

            foreach (var cmd in pendingCommands) {
                if (IsCoveredByActiveFallback(cmd.pos, cmd.lod)) {
                    continue;
                }

                DrawChunk(cmd.chunk, cmd.pos, cmd.visibility);
            }
        }

        private void CollectLodRegion(
            int lod,
            IndexPos viewLod,
            int chunkSize,
            int size,
            IndexPos? excludeViewLod,
            int excludeSize,
            int excludeChunkSize,
            Vector3 lightDir
        ) {
            if (!GameManager.Instance.controlRender[lod]) return;
            
            float halfSize = chunkSize * 0.5f;
            var extents = new Vector3(halfSize, halfSize, halfSize);

            for (int y = 0; y < size; y++)
            for (int z = 0; z < size; z++)
            for (int x = 0; x < size; x++) {

                var pos = viewLod + new IndexPos(x, y, z) * chunkSize;

                if (pos.x < 0 || pos.y < 0 || pos.z < 0) continue;

                if (excludeViewLod.HasValue && IsInside(pos, excludeViewLod.Value, excludeSize, excludeChunkSize)) {
                    continue;
                }

                var center = new Vector3(pos.x + halfSize, pos.y + halfSize, pos.z + halfSize);
                var visibility = EvaluateVisibility(center, extents, lightDir);

                if (visibility == RenderVisibility.Hidden) continue;
                    
                var chunk = worldManager.FindChunkData(lod, pos);
                
                if (chunk?.IsEmpty == true) continue;

                if (chunk == null || chunk.Mesh == null) {
                    TryCollectFallback(pos, lod, lightDir);
                    continue;
                }

                pendingCommands.Add(new RenderCommand {
                    chunk = chunk,
                    pos = pos,
                    lod = lod,
                    visibility = visibility
                });
            }
        }
        
        private void TryCollectFallback(IndexPos missingPos, int lod, Vector3 lightDir) {
            // ------------------------------------------------------------------------
            // Lower LOD (lod - 1)
            // ------------------------------------------------------------------------
            if (lod > 0) {
                int lowerLod = lod - 1;
                int lowerChunkSize = 32 << lowerLod;
                bool foundAllSubChunks = true;

                int index = 0;

                for (int x = 0; x < 2 && foundAllSubChunks; x++)
                for (int y = 0; y < 2 && foundAllSubChunks; y++)
                for (int z = 0; z < 2 && foundAllSubChunks; z++) {
                    var subPos = missingPos + new IndexPos(x, y, z) * lowerChunkSize;
                    var lowerChunk = worldManager.FindChunkData(lowerLod, subPos);

                    if (lowerChunk != null && (lowerChunk.IsEmpty || lowerChunk.Mesh != null)) {
                        subChunks[index] = lowerChunk;
                        subPositions[index] = subPos;
                        index++;
                    } else {
                        foundAllSubChunks = false;
                    }
                }

                if (foundAllSubChunks) {
                    float lHalfSize = lowerChunkSize * 0.5f;
                    var lExtents = new Vector3(lHalfSize, lHalfSize, lHalfSize);

                    for (int i = 0; i < 8; i++) {
                        var subChunk = subChunks[i];
                        if (subChunk.IsEmpty) continue;

                        var subPos = subPositions[i];
                        var center = new Vector3(subPos.x + lHalfSize, subPos.y + lHalfSize, subPos.z + lHalfSize);
                        var subVis = EvaluateVisibility(center, lExtents, lightDir);

                        if (subVis != RenderVisibility.Hidden) {
                            pendingCommands.Add(new RenderCommand {
                                chunk = subChunk,
                                pos = subPos,
                                lod = lowerLod,
                                visibility = subVis
                            });
                        }
                    }

                    for (int i = 0; i < subChunks.Length; i++) {
                        subChunks[i] = null;
                    }
                    return;
                } else {
                    for (int i = 0; i < subChunks.Length; i++) {
                        subChunks[i] = null;
                    }
                }
            }

            // ------------------------------------------------------------------------
            // Higher LOD (lod + 1)
            // ------------------------------------------------------------------------
            if (lod < Region.TotalLods - 1) {
                int higherLod = lod + 1;
                int higherChunkSize = 32 << higherLod;

                IndexPos parentPos = new IndexPos(
                    Mathf.FloorToInt((float)missingPos.x / higherChunkSize) * higherChunkSize,
                    Mathf.FloorToInt((float)missingPos.y / higherChunkSize) * higherChunkSize,
                    Mathf.FloorToInt((float)missingPos.z / higherChunkSize) * higherChunkSize
                );

                if (activeFallbackParentPositions.Contains(parentPos)) {
                    return;
                }

                var higherChunk = worldManager.FindChunkData(higherLod, parentPos);

                if (higherChunk != null && (higherChunk.IsEmpty || higherChunk.Mesh != null)) {
                    activeFallbackParentPositions.Add(parentPos);

                    if (higherChunk.IsEmpty) return;
                    
                    float hHalfSize = higherChunkSize * 0.5f;
                    var hExtents = new Vector3(hHalfSize, hHalfSize, hHalfSize);
                    var center = new Vector3(parentPos.x + hHalfSize, parentPos.y + hHalfSize, parentPos.z + hHalfSize);

                    var higherVis = EvaluateVisibility(center, hExtents, lightDir);
                    if (higherVis != RenderVisibility.Hidden) {
                        pendingCommands.Add(new RenderCommand {
                            chunk = higherChunk,
                            pos = parentPos,
                            lod = higherLod,
                            visibility = higherVis
                        });
                    }
                    return;
                }
            }

            // ------------------------------------------------------------------------
            // Fallback for incomplete Sub-chunks
            // ------------------------------------------------------------------------
            if (lod > 0) {
                int lowerLod = lod - 1;
                int lowerChunkSize = 32 << lowerLod;
                float lHalfSize = lowerChunkSize * 0.5f;
                var lExtents = new Vector3(lHalfSize, lHalfSize, lHalfSize);

                for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                for (int z = 0; z < 2; z++) {
                    var subPos = missingPos + new IndexPos(x, y, z) * lowerChunkSize;
                    var lowerChunk = worldManager.FindChunkData(lowerLod, subPos);

                    if (lowerChunk == null || lowerChunk.Mesh == null) continue;
                    
                    var center = new Vector3(subPos.x + lHalfSize, subPos.y + lHalfSize, subPos.z + lHalfSize);
                    var subVis = EvaluateVisibility(center, lExtents, lightDir);

                    if (subVis != RenderVisibility.Hidden) {
                        pendingCommands.Add(new RenderCommand {
                            chunk = lowerChunk,
                            pos = subPos,
                            lod = lowerLod,
                            visibility = subVis
                        });
                    }
                }
            }
        }

        private bool IsCoveredByActiveFallback(IndexPos pos, int currentLod) {
            if (activeFallbackParentPositions.Count == 0) return false;

            for (int higherLod = currentLod + 1; higherLod < Region.TotalLods; higherLod++) {
                int higherChunkSize = 32 << higherLod;

                IndexPos parentPos = new IndexPos(
                    Mathf.FloorToInt((float)pos.x / higherChunkSize) * higherChunkSize,
                    Mathf.FloorToInt((float)pos.y / higherChunkSize) * higherChunkSize,
                    Mathf.FloorToInt((float)pos.z / higherChunkSize) * higherChunkSize
                );

                if (activeFallbackParentPositions.Contains(parentPos)) {
                    return true;
                }
            }

            return false;
        }

        private enum RenderVisibility {
            Hidden,
            ShadowOnly,
            VisibleInFrustum
        }

        private RenderVisibility EvaluateVisibility(Vector3 center, Vector3 extents, Vector3 lightDir) {
            if (TestAABBFast(center, extents)) {
                return RenderVisibility.VisibleInFrustum;
            }

            if (cachedShadowDistance > 0f) {
                var shadowOffset = -lightDir * (cachedShadowDistance * 0.5f);
                var shadowCenter = center + shadowOffset;
                
                var shadowExtents = extents + new Vector3(
                    Mathf.Abs(shadowOffset.x),
                    Mathf.Abs(shadowOffset.y),
                    Mathf.Abs(shadowOffset.z)
                );

                if (TestAABBFast(shadowCenter, shadowExtents)) {
                    return RenderVisibility.ShadowOnly;
                }
            }

            return RenderVisibility.Hidden;
        }

        private bool TestAABBFast(Vector3 center, Vector3 extents) {
            for (int i = 0; i < 6; i++) {
                var plane = frustumPlanes[i];
                var normal = plane.normal;

                float r = extents.x * Mathf.Abs(normal.x) +
                          extents.y * Mathf.Abs(normal.y) +
                          extents.z * Mathf.Abs(normal.z);

                float distance = normal.x * center.x + normal.y * center.y + normal.z * center.z + plane.distance;

                if (distance < -r) {
                    return false;
                }
            }
            return true;
        }

        private void DrawChunk(ChunkRenderData chunk, IndexPos pos, RenderVisibility visibility) {
            var matrix = Matrix4x4.TRS((Vector3)pos, Quaternion.identity, Vector3.one * (1 << chunk.Lod));
            var mesh = chunk.Mesh == null ? GameManager.Instance.smallCube : chunk.Mesh;
            if (visibility == RenderVisibility.VisibleInFrustum) {
                Graphics.RenderMesh(visibleRenderParams, mesh, 0, matrix);
            } else if (visibility == RenderVisibility.ShadowOnly) {
                Graphics.RenderMesh(shadowOnlyRenderParams, mesh, 0, matrix);
            }
        }

        private bool IsInside(IndexPos pos, IndexPos min, int size, int chunkSize) {
            var max = min + new IndexPos(size, size, size) * chunkSize;

            return pos.x >= min.x && pos.x < max.x &&
                   pos.y >= min.y && pos.y < max.y &&
                   pos.z >= min.z && pos.z < max.z;
        }

        public void RequestMesh(Chunk[] chunks, Action<Mesh> action) {
            GameManager.Instance.MeshGenerator.Remesh(chunks, action);
        }
    }
}