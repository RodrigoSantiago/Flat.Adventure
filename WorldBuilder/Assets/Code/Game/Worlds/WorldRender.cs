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
        private readonly Plane[][] frustumShadowPlanes = new Plane[Region.TotalLods][];
        private Vector3 lightDir;
        private float cachedShadowDistance;

        private struct RenderCommand {
            public ChunkRenderData chunk;
            public IndexPos pos;
            public int lod;
            public RenderVisibility visibility;
        }

        private readonly List<RenderCommand> pendingCommands = new ();
        
        private MeshRenderBuffer completeRenderBuffer = new();
        private MeshRenderBuffer colorOnlyRenderBuffer = new();
        private MeshRenderBuffer shadowOnlyRenderBuffer = new();
        private RenderParams completeRenderParams;
        private RenderParams colorOnlyRenderParams;
        private RenderParams shadowOnlyRenderParams;

        public WorldRender(WorldManager worldManager) {
            this.worldManager = worldManager;
            for (int i = 0; i < frustumShadowPlanes.Length; i++) {
                frustumShadowPlanes[i] = new Plane[6];
            }
        }

        public void Dispose() {
            completeRenderBuffer.Dispose();
            colorOnlyRenderBuffer.Dispose();
            shadowOnlyRenderBuffer.Dispose();
        }

        public Camera GetActiveCamera() {
            return GameManager.Instance.PlayerCamera;
        }

        public void RenderFrame() {
            Camera cam = GetActiveCamera();
            if (cam == null) return;

            if (GameManager.Instance.terrainShadow) {
                var urp = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
                cachedShadowDistance = urp != null ? urp.shadowDistance : QualitySettings.shadowDistance;
            } else {
                cachedShadowDistance = 0;
            }

            GeometryUtility.CalculateFrustumPlanes(cam, frustumPlanes);
            if (cachedShadowDistance > 0) {
                for (int i = 0; i < frustumShadowPlanes.Length; i++) {
                    GeometryUtility.CalculateFrustumPlanes(
                        Matrix4x4.Perspective(
                            cam.fieldOfView,
                            cam.aspect,
                            cam.nearClipPlane,
                            cachedShadowDistance
                        ) * cam.worldToCameraMatrix, frustumShadowPlanes[i]);
                    ExpandFrustum(frustumShadowPlanes[i], (32 << i));
                }
            }
            
            int terrainLayer = LayerMask.NameToLayer("TerrainChunk");
            completeRenderParams = new RenderParams(GameManager.Instance.MeshGenerator.GroundMaterial) {
                layer = terrainLayer,
                receiveShadows = true,
                shadowCastingMode = ShadowCastingMode.On,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 100000)
            };
            completeRenderBuffer.Begin(completeRenderParams);
            
            colorOnlyRenderParams = new RenderParams(GameManager.Instance.MeshGenerator.GroundMaterial) {
                layer = terrainLayer,
                receiveShadows = true,
                shadowCastingMode = ShadowCastingMode.Off,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 100000)
            };
            colorOnlyRenderBuffer.Begin(colorOnlyRenderParams);

            shadowOnlyRenderParams = new RenderParams(GameManager.Instance.MeshGenerator.DummyMaterial) {
                layer = terrainLayer,
                receiveShadows = false,
                shadowCastingMode = GameManager.Instance.debugShadow ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 100000)
            };
            shadowOnlyRenderBuffer.Begin(shadowOnlyRenderParams);

            lightDir = RenderSettings.sun != null ? RenderSettings.sun.transform.forward.normalized : Vector3.down;

            int lod = 3;
            int size = (32 << lod);
            var config = worldManager.lodConfig[3];
            for (int y = 0; y < config.Height; y++)
            for (int z = 0; z < config.Size; z++)
            for (int x = 0; x < config.Size; x++) {
                var pos = config.ViewLod + new IndexPos(x, y, z) * size;
                RenderRegion(lod, pos, pendingCommands);
            }
            
            foreach (var cmd in pendingCommands) {
                if (GameManager.Instance.controlRender[cmd.chunk.Lod]) {
                    DrawChunk(cmd.chunk, cmd.pos, cmd.visibility);
                }
            }
            completeRenderBuffer.Commit();
            colorOnlyRenderBuffer.Commit();
            shadowOnlyRenderBuffer.Commit();
            completeRenderBuffer.Render();
            if (GameManager.Instance.terrainColorOnly) {
                colorOnlyRenderBuffer.Render();
            }

            if (GameManager.Instance.terrainShadow) {
                shadowOnlyRenderBuffer.Render();
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
                if (pos.y >= Region.MaxHeight || !config.IsInside(pos)) continue;
                
                CollectRegion(lod, pos, tempCollectChunks);
            }

            foreach (var chunk in tempCollectChunks) {
                renderedPositions[chunk.Lod].Add(chunk.Pos);
            }
            tempCollectChunks.Clear();
        }

        private bool RenderRegion(int lod, IndexPos pos, List<RenderCommand> chunks) {
            int lSize = (32 << lod);
            int lHSize = lSize >> 1;
            var visibility = EvaluateVisibility((Vector3)(pos + lHSize), new Vector3(lSize, lSize, lSize), lod);
            
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
                    bool complete = RenderRegion(lod - 1, pos + new IndexPos(x, y, z) * size, chunks);
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
            ColorOnly,
            Complete
        }

        private RenderVisibility EvaluateVisibility(Vector3 center, Vector3 size, int lod) {
            if (!GameManager.Instance.frustumOcclusion) return RenderVisibility.Complete;

            var bounds = new Bounds(center, size);
            if (GeometryUtility.TestPlanesAABB(frustumPlanes, bounds)) {
                if (cachedShadowDistance > 0f) {
                    if (RayIntersectsFrustum(center, lightDir, frustumShadowPlanes[lod])) {
                        return RenderVisibility.Complete;
                    }
                }

                return RenderVisibility.ColorOnly;
            }

            if (cachedShadowDistance > 0f) {
                if (RayIntersectsFrustum(center, lightDir, frustumShadowPlanes[lod])) {
                    return RenderVisibility.ShadowOnly;
                }
            }

            return RenderVisibility.Hidden;
        }

        static bool RayIntersectsFrustum(Vector3 origin, Vector3 direction, Plane[] planes) {
            float tMin = 0f;
            float tMax = float.MaxValue;

            for (int i = 0; i < planes.Length; i++) {
                Vector3 normal = planes[i].normal;
                float planeDistance = planes[i].distance;

                float denominator = Vector3.Dot(normal, direction);
                float originDistance = Vector3.Dot(normal, origin) + planeDistance;

                if (denominator > -1e-6f && denominator < 1e-6f) {
                    if (originDistance < 0f) {
                        return false;
                    }
                } else {
                    float t = -originDistance / denominator;

                    if (denominator > 0f) {
                        if (t > tMin) {
                            tMin = t;
                        }
                    } else {
                        if (t < tMax) {
                            tMax = t;
                        }
                    }
                    if (tMin > tMax) {
                        return false;
                    }
                }
            }

            return true;
        }

        static void ExpandFrustum(Plane[] planes, float amount) {
            for (int i = 0; i < 6; i++) {
                Plane p = planes[i];
                p.distance += amount;
                planes[i] = p;
            }
        }
        
        private void DrawChunk(ChunkRenderData chunk, IndexPos pos, RenderVisibility visibility) {
            if (chunk.IsEmpty) return;
            if (chunk.SoilMesh == null) {
                var matrix = Matrix4x4.TRS((Vector3)pos, Quaternion.identity, Vector3.one * (1 << chunk.Lod));
                if (visibility == RenderVisibility.Complete) {
                    Graphics.RenderMesh(completeRenderParams, GameManager.Instance.smallCube, 0, matrix);
                } else if (visibility == RenderVisibility.ShadowOnly) {
                    Graphics.RenderMesh(shadowOnlyRenderParams, GameManager.Instance.smallCube, 0, matrix);
                }
            } else {
                if (visibility == RenderVisibility.Complete) {
                    chunk.SoilMesh.Render(completeRenderBuffer);
                } else if (visibility == RenderVisibility.ColorOnly) {

                    if (GameManager.Instance.terrainColorOnly) {
                        chunk.SoilMesh.Render(colorOnlyRenderBuffer);
                    } else {
                        chunk.SoilMesh.Render(completeRenderBuffer);
                    }
                } else if (visibility == RenderVisibility.ShadowOnly) {
                    chunk.SoilMesh.Render(shadowOnlyRenderBuffer);
                }
            }
        }

        public void RequestMesh(Chunk[] chunks, Action<MeshInterface> action) {
            GameManager.Instance.MeshGenerator.Remesh(chunks, action);
        }
    }
}