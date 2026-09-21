using System;
using System.Collections;
using Game.Data;
using Game.Worlds.Rendering;
using System.Collections.Generic;
using Game.GraphicGenerator;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
        
        private RenderParams visibleRenderParams;
        private RenderParams shadowOnlyRenderParams;

        public WorldRender(WorldManager worldManager) {
            this.worldManager = worldManager;
        }

        public Camera GetActiveCamera() {
            return GameManager.Instance.PlayerCamera;
        }

        public void RenderFrame() {
            Camera cam = GetActiveCamera();
            if (cam == null) return;

            GeometryUtility.CalculateFrustumPlanes(cam, frustumPlanes);
            
            var urp = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            cachedShadowDistance = urp != null ? urp.shadowDistance : QualitySettings.shadowDistance;
            
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

            var lightDir = RenderSettings.sun != null ? RenderSettings.sun.transform.forward : Vector3.down;

            int lod = 3;
            int size = (32 << lod);
            var config = worldManager.lodConfig[3];
            for (int y = 0; y < config.Height; y++)
            for (int z = 0; z < config.Size; z++)
            for (int x = 0; x < config.Size; x++) {
                var pos = config.ViewLod + new IndexPos(x, y, z) * size;
                RenderRegion(lod, pos, pendingCommands, lightDir);
            }
            
            foreach (var cmd in pendingCommands) {
                if (GameManager.Instance.controlRender[cmd.chunk.Lod]) {
                    DrawChunk(cmd.chunk, cmd.pos, cmd.visibility);
                }
            }
            pendingCommands.Clear();
        }

        private List<ChunkRenderData> tempCollectChunks = new ();
        
        public void CollectFrame(HashSet<IndexPos>[] renderedPositions) {
            int lod = 3;
            int size = (32 << lod);
            var config = worldManager.lodConfig[3];
            for (int y = 0; y < config.Height; y++)
            for (int z = 0; z < config.Size; z++)
            for (int x = 0; x < config.Size; x++) {
                var pos = config.ViewLod + new IndexPos(x, y, z) * size;
                if (!config.IsInside(pos)) continue;
                
                CollectRegion(lod, pos, tempCollectChunks);
            }

            foreach (var chunk in tempCollectChunks) {
                renderedPositions[chunk.Lod].Add(chunk.Pos);
            }
            tempCollectChunks.Clear();
        }

        private bool RenderRegion(int lod, IndexPos pos, List<RenderCommand> chunks, Vector3 lightDir) {
            int lSize = (32 << lod);
            int lHSize = lSize >> 1;
            var visibility = EvaluateVisibility((Vector3)(pos + lHSize), new Vector3(lHSize, lHSize, lHSize), lightDir);
            
            if (visibility == RenderVisibility.Hidden) return true;
            
            var chunk = worldManager.FindChunkData(lod, pos);
            bool highLodFound = (chunk != null && (chunk.IsEmpty || chunk.SoilMesh != null));
            bool findLowerLods = lod > 0 
                                 && (!highLodFound || (worldManager.lodConfig[lod - 1].IsInside(pos, lSize)))
                                 && worldManager.HasAnyChunkData(pos);

            if (findLowerLods) {
                int bkp = chunks.Count;
                int size = (32 << (lod - 1));
                bool lCompete = true;
                for (int y = 0; y < 2; y++)
                for (int z = 0; z < 2; z++)
                for (int x = 0; x < 2; x++) {
                    bool complete = RenderRegion(lod - 1, pos + new IndexPos(x, y, z) * size, chunks, lightDir);
                    if (!complete) {
                        lCompete = false;
                        if (highLodFound) {
                            y = z = 2;
                            break;
                        }
                    }
                }

                if (lCompete || !highLodFound) {
                    return lCompete;
                }
                chunks.RemoveRange(bkp, chunks.Count - bkp);
            } 
            
            if (highLodFound) {
                if (!chunk.IsEmpty) {
                    chunks.Add(new RenderCommand {
                        chunk = chunk,
                        pos = pos,
                        lod = lod,
                        visibility = visibility
                    });
                }

                return true;
            }
            
            return false;
        }

        private bool CollectRegion(int lod, IndexPos pos, List<ChunkRenderData> chunks) {
            int lSize = (32 << lod);
            
            var chunk = worldManager.FindChunkData(lod, pos);
            bool highLodFound = (chunk != null && (chunk.IsEmpty || chunk.SoilMesh != null));
            bool findLowerLods = lod > 0 
                                 && (!highLodFound || (worldManager.lodConfig[lod - 1].IsInside(pos, lSize)))
                                 && worldManager.HasAnyChunkData(pos);

            if (findLowerLods) {
                int bkp = chunks.Count;
                int size = (32 << (lod - 1));
                bool lCompete = true;
                for (int y = 0; y < 2; y++)
                for (int z = 0; z < 2; z++)
                for (int x = 0; x < 2; x++) {
                    bool complete = CollectRegion(lod - 1, pos + new IndexPos(x, y, z) * size, chunks);
                    if (!complete) {
                        lCompete = false;
                        if (highLodFound) {
                            y = z = 2;
                            break;
                        }
                    }
                }

                if (lCompete || !highLodFound) {
                    return lCompete;
                }
                chunks.RemoveRange(bkp, chunks.Count - bkp);
                
            } 
            
            if (highLodFound) {
                chunks.Add(chunk);
                return true;
            }
            
            return false;
        }

        private enum RenderVisibility {
            Hidden,
            ShadowOnly,
            VisibleInFrustum
        }

        private RenderVisibility EvaluateVisibility(Vector3 center, Vector3 extents, Vector3 lightDir) {
            if (TestAABBFast(center, extents) || !GameManager.Instance.frustumOcclusion) {
                return RenderVisibility.VisibleInFrustum;
            }

            if (cachedShadowDistance > 0f) {
                var shadowOffset = lightDir * (cachedShadowDistance * 0.5f);
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
            if (chunk.IsEmpty) return;
            if (chunk.SoilMesh == null) {
                var matrix = Matrix4x4.TRS((Vector3)pos, Quaternion.identity, Vector3.one * (1 << chunk.Lod));
                if (visibility == RenderVisibility.VisibleInFrustum) {
                    Graphics.RenderMesh(visibleRenderParams, GameManager.Instance.smallCube, 0, matrix);
                } else if (visibility == RenderVisibility.ShadowOnly) {
                    Graphics.RenderMesh(shadowOnlyRenderParams, GameManager.Instance.smallCube, 0, matrix);
                }
            } else {
                if (visibility == RenderVisibility.VisibleInFrustum) {
                    chunk.SoilMesh.Render(visibleRenderParams);
                } else if (visibility == RenderVisibility.ShadowOnly) {
                    chunk.SoilMesh.Render(shadowOnlyRenderParams);
                }
            }
        }

        public void RequestMesh(Chunk[] chunks, Action<MeshInterface> action) {
            GameManager.Instance.MeshGenerator.Remesh(chunks, action);
        }
    }
}