using System;
using Adventure.Logic.Data;
using UnityEngine;

namespace Adventure.Game.Manager.ChunkManagment {
    
    [Serializable]
    public struct ChunkCollisionHit {
        public bool hit;
        public Vector3 start;
        public Vector3 dir;
        public float distance;
        public Vector3 collision;
        public Vector3 normal;
        public Voxel voxel;

        public ChunkCollisionHit(bool hit, Vector3 start, Vector3 dir, float distance, Vector3 collision, Vector3 normal, Voxel voxel) {
            this.hit = hit;
            this.start = start;
            this.dir = dir;
            this.distance = distance;
            this.collision = collision;
            this.normal = normal;
            this.voxel = voxel;
        }
        
        
        public static implicit operator bool(ChunkCollisionHit foo) {
            return foo.hit;
        }
    }
}