using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Utils {

    [BurstCompile(CompileSynchronously = true)]
    public static class UtilsCulling {

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TestAABBFrustum(
            in float3 center,
            in float3 extent,
            in float4 planeX03, in float4 planeY03, in float4 planeZ03, in float4 planeD03,
            in float4 planeX45, in float4 planeY45, in float4 planeZ45, in float4 planeD45) {

            // (SIMD)
            float4 dist03 = planeX03 * center.x + planeY03 * center.y + planeZ03 * center.z + planeD03;
            float4 radius03 = math.abs(planeX03) * extent.x + math.abs(planeY03) * extent.y +
                              math.abs(planeZ03) * extent.z;

            if (math.any(dist03 + radius03 < 0f)) return false;

            float4 dist45 = planeX45 * center.x + planeY45 * center.y + planeZ45 * center.z + planeD45;
            float4 radius45 = math.abs(planeX45) * extent.x + math.abs(planeY45) * extent.y +
                              math.abs(planeZ45) * extent.z;

            if (math.any(dist45 + radius45 < 0f)) return false;

            return true;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TestAABBFrustum(
            in float3 center,
            in float3 extent, in FrustumPlanesSoA planes) {
            return TestAABBFrustum(in center, in extent,
                in planes.x03, in planes.y03, in planes.z03, in planes.d03,
                in planes.x45, in planes.y45, in planes.z45, in planes.d45
            );
        }
        
        //----
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RayIntersectsFrustum(
            in float3 origin,
            in float3 direction,
            in FrustumPlanesSoA planes) {

            float tMin = 0f;
            float tMax = float.MaxValue;

            if (!EvaluateRayBlock(origin, direction, planes.x03, planes.y03, planes.z03, planes.d03, ref tMin, ref tMax))
                return false;

            if (!EvaluateRayBlock45(origin, direction, planes.x45, planes.y45, planes.z45, planes.d45, ref tMin, ref tMax))
                return false;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool EvaluateRayBlock(
            in float3 origin, in float3 direction,
            in float4 px, in float4 py, in float4 pz, in float4 pd,
            ref float tMin, ref float tMax) {

            // SIMD Dot products 4 planes
            float4 denom = px * direction.x + py * direction.y + pz * direction.z;
            float4 origDist = px * origin.x + py * origin.y + pz * origin.z + pd;

            // (|denom| < 1e-6)
            bool4 isParallel = math.abs(denom) < 1e-6f;

            if (math.any(isParallel & (origDist < 0f))) return false;

            float4 t = -origDist / math.select(denom, 1f, isParallel);

            bool4 maskValid = !isParallel;

            for (int i = 0; i < 4; i++) {
                if (!maskValid[i]) continue;

                float ti = t[i];
                if (denom[i] > 0f) {
                    if (ti > tMin) tMin = ti;
                } else {
                    if (ti < tMax) tMax = ti;
                }

                if (tMin > tMax) return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool EvaluateRayBlock45(
            in float3 origin, in float3 direction,
            in float4 px, in float4 py, in float4 pz, in float4 pd,
            ref float tMin, ref float tMax) {

            float4 denom = px * direction.x + py * direction.y + pz * direction.z;
            float4 origDist = px * origin.x + py * origin.y + pz * origin.z + pd;

            bool4 isParallel = math.abs(denom) < 1e-6f;

            if ((isParallel.x && origDist.x < 0f) || (isParallel.y && origDist.y < 0f))
                return false;

            float4 t = -origDist / math.select(denom, 1f, isParallel);

            // Plane 4
            if (!isParallel.x) {
                if (denom.x > 0f) { if (t.x > tMin) tMin = t.x; }
                else { if (t.x < tMax) tMax = t.x; }
            }

            // Plane 5
            if (!isParallel.y) {
                if (denom.y > 0f) { if (t.y > tMin) tMin = t.y; }
                else { if (t.y < tMax) tMax = t.y; }
            }

            return tMin <= tMax;
        }
    }
        
    public struct FrustumPlanesSoA {
        public float4 x03, y03, z03, d03;
        public float4 x45, y45, z45, d45;

        public void UpdatePlanes(Plane[] planes) {
            x03 = new float4(planes[0].normal.x, planes[1].normal.x, planes[2].normal.x, planes[3].normal.x);
            y03 = new float4(planes[0].normal.y, planes[1].normal.y, planes[2].normal.y, planes[3].normal.y);
            z03 = new float4(planes[0].normal.z, planes[1].normal.z, planes[2].normal.z, planes[3].normal.z);
            d03 = new float4(planes[0].distance, planes[1].distance, planes[2].distance, planes[3].distance);

            x45 = new float4(planes[4].normal.x, planes[5].normal.x, 0, 0);
            y45 = new float4(planes[4].normal.y, planes[5].normal.y, 0, 0);
            z45 = new float4(planes[4].normal.z, planes[5].normal.z, 0, 0);
            d45 = new float4(planes[4].distance, planes[5].distance, 100000f, 100000f);
        }
    }
}