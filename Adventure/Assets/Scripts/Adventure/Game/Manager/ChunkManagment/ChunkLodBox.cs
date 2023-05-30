using System;
using UnityEngine;

namespace Adventure.Game.Manager.ChunkManagment {
    [Serializable]
    public struct ChunkLodBox {
        public int lod;

        public Vector3Int min;
        public Vector3Int size;
        public Vector3Int max { get => min + size; }

        public ChunkLodBox(int lod, Vector3Int min = default, Vector3Int size = default) {
            this.lod = lod;
            this.min = min;
            this.size = size;
        }

        public bool Contains(Vector3Int local) {
            return local.x >= min.x && local.x < size.x + min.x &&
                   local.y >= min.y && local.y < size.y + min.y &&
                   local.z >= min.z && local.z < size.z + min.z;
        }

        public bool LodLimit(Vector3Int local, out Vector3Int minLimit, out Vector3Int maxLimit) {
            int minx = local.x == min.x ? 1 : 0;
            int maxx = local.x + 16 * (1 << lod) == size.x + min.x ? 1 : 0;
            
            int miny = local.y == min.y ? 1 : 0;
            int maxy = local.y + 16 * (1 << lod) == size.y + min.y ? 1 : 0;
            
            int minz = local.z == min.z ? 1 : 0;
            int maxz = local.z + 16 * (1 << lod) == size.z + min.z ? 1 : 0;
            
            minLimit = new Vector3Int(minx, miny, minz);
            maxLimit = new Vector3Int(maxx, maxy, maxz);
            return minLimit != Vector3Int.zero || maxLimit != Vector3Int.zero;
        }
    }
}