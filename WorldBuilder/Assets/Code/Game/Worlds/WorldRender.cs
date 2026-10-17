using System;
using Game.Data;
using Game.Worlds.Rendering;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Game.GraphicGenerator;
using Game.Utils;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Worlds {
    public class WorldRender {
        private readonly WorldManager worldManager;

        private readonly Plane[] frustumPlanes = new Plane[6];
        public static readonly Plane[][] frustumShadowPlanes = new Plane[Region.TotalLods][];
        private FrustumPlanesSoA frustumPlanesSoA = new FrustumPlanesSoA();
        private readonly FrustumPlanesSoA[] frustumShadowPlanesSoA = new FrustumPlanesSoA[Region.TotalLods];
        
        private float3 lightDir;
        private float cachedShadowDistance;

        private enum RenderVisibility {
            Hidden,
            ShadowOnly,
            ColorOnly,
            Complete
        }

        private struct RenderCommand {
            public ChunkRenderData chunk;
            public IndexPos pos;
            public RenderVisibility visibility;
        }

        private readonly List<List<RenderCommand>> renderCommands = new ();
        private readonly List<List<IndexLodPos>> collectedCommands = new ();
        
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
            frustumPlanesSoA.UpdatePlanes(frustumPlanes);
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
                    frustumShadowPlanesSoA[i].UpdatePlanes(frustumShadowPlanes[i]);
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

            int lod = worldManager.maxLodView;
            int size = (32 << lod);
            var config = worldManager.lodConfig[lod];

            int minHeight = -Math.Min(0, config.ViewLod.y) / config.ChunkSize;
            int maxHeight = (Math.Min(Region.MaxHeight, config.Max.y) - config.ViewLod.y) / config.ChunkSize;
            while (renderCommands.Count < maxHeight) {
                renderCommands.Add(new List<RenderCommand>());
            }

            UtilsParallel.For(minHeight, maxHeight, y => {
                // for (int y = minHeight; y < maxHeight; y++)
                for (int z = 0; z < config.Size; z++)
                for (int x = 0; x < config.Size; x++) {
                    var pos = config.ViewLod + new IndexPos(x, y, z) * size;
                    if (lod > 0 && !config.IsInsideCylinder(pos)) continue;

                    RenderRegion(lod, pos, renderCommands[y]);
                }
            });
            
            foreach (var list in renderCommands) {
                foreach (var cmd in list) {
                    DrawChunk(cmd.chunk, cmd.pos, cmd.visibility);
                }
                list.Clear();
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
        }

        private bool RenderRegion(int lod, IndexPos pos, List<RenderCommand> chunks) {
            int lSize = (32 << lod);
            int hSize = (16 << lod);
            var visibility = EvaluateVisibility(
                new float3(pos.x + hSize, pos.y + hSize, pos.z + hSize), 
                new float3(hSize, hSize, hSize), lod);
            
            if (visibility == RenderVisibility.Hidden) return true;
            
            var chunk = worldManager.FindChunkData(lod, pos);
            bool highLodFound = (chunk != null && (chunk.IsEmpty || chunk.SoilMesh != null));
            bool findLowerLods = lod > 0 
                                 && (!highLodFound || (worldManager.lodConfig[lod - 1].IsInside(pos, lSize)))
                                 && (lod < worldManager.maxLodView || worldManager.HasAnyInRegion(pos));

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
                        visibility = visibility
                    });
                }

                return true;
            }
            
            return false;
        }
        
        public void CollectFrame(HashSet<IndexPos>[] renderedPositions) {
            int lod = worldManager.maxLodView;
            int size = (32 << lod);
            var config = worldManager.lodConfig[lod];
            
            int minHeight = -Math.Min(0, config.ViewLod.y) / config.ChunkSize;
            int maxHeight = (Math.Min(Region.MaxHeight, config.Max.y) - config.ViewLod.y) / config.ChunkSize;
            while (collectedCommands.Count < maxHeight) {
                collectedCommands.Add(new List<IndexLodPos>());
            }

            UtilsParallel.For(minHeight, maxHeight, y => {
                // for (int y = 0; y < maxHeight; y++)
                for (int z = 0; z < config.Size; z++)
                for (int x = 0; x < config.Size; x++) {
                    var pos = config.ViewLod + new IndexPos(x, y, z) * size;
                    if (lod > 0 && !config.IsInsideCylinder(pos)) continue;

                    CollectRegion(lod, pos, collectedCommands[y]);
                }
            });

            foreach (var list in collectedCommands) {
                foreach (var cmd in list) {
                    renderedPositions[cmd.lod].Add(cmd.pos);
                }
            }
            foreach (var list in collectedCommands) {
                list.Clear();
            }
        }

        private bool CollectRegion(int lod, IndexPos pos, List<IndexLodPos> chunks) {
            int lSize = (32 << lod);
            
            var chunk = worldManager.FindChunkData(lod, pos);
            bool highLodFound = (chunk != null && (chunk.IsEmpty || chunk.SoilMesh != null));
            bool findLowerLods = lod > 0 
                                 && (!highLodFound || (worldManager.lodConfig[lod - 1].IsInside(pos, lSize)))
                                 && (lod < worldManager.maxLodView || worldManager.HasAnyInRegion(pos));

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
                chunks.Add(new IndexLodPos(chunk.Pos, chunk.Lod));
                return true;
            }
            
            return false;
        }

        private RenderVisibility EvaluateVisibility(float3 center, float3 extent, int lod) {
            #if UNITY_EDITOR
            if (!GameManager.Instance.frustumOcclusion) return RenderVisibility.Complete;
            #endif
            
            if (UtilsCulling.TestAABBFrustum(in center, in extent, in frustumPlanesSoA)) {
                if (cachedShadowDistance > 0f) {
                    if (UtilsCulling.RayIntersectsFrustum(in center, in lightDir, in frustumShadowPlanesSoA[lod])) {
                        return RenderVisibility.Complete;
                    }
                }

                return RenderVisibility.ColorOnly;
            }

            if (cachedShadowDistance > 0f) {
                if (UtilsCulling.RayIntersectsFrustum(in center, in lightDir, in frustumShadowPlanesSoA[lod])) {
                    return RenderVisibility.ShadowOnly;
                }
            }

            return RenderVisibility.Hidden;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool TestPlanesAABB(Plane[] planes, Vector3 min, Vector3 max) {
            for (int i = 0; i < 6; i++) {
                Vector3 n = planes[i].normal;
                float x = n.x >= 0f ? max.x : min.x;
                float y = n.y >= 0f ? max.y : min.y;
                float z = n.z >= 0f ? max.z : min.z;

                if (n.x * x + n.y * y + n.z * z + planes[i].distance < 0f) return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                planes[i].distance += amount;
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
            GameManager.Instance.MeshGenerator.Remesh(worldManager, chunks, action);
        }
    }
}