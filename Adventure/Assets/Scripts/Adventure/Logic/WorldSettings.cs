using System;
using UnityEngine;

namespace Adventure.Logic {
    public readonly struct WorldSettings {
        public readonly int width;
        public readonly int height;
        public readonly int length;

        public WorldSettings(int width, int height, int length) {
            this.width = width;
            this.height = height;
            this.length = length;
        }

        public Vector3Int Pos(Vector3Int pos) {
            if (pos.x < 0) pos.x = (pos.x + width * 100) % width;
            if (pos.x >= width) pos.x = pos.x % width;
            if (pos.z < 0) pos.z = (pos.z + length * 100) % length;
            if (pos.z >= length) pos.z = pos.z % length;
            return pos;
        }

        public Vector3Int CloserPos(Vector3Int pos, Vector3Int reference) {
            if (Math.Abs(pos.x - width - reference.x) < Math.Abs(pos.x - reference.x)) {
                pos.x -= width;
            }  else if (Math.Abs(pos.x + width - reference.x) < Math.Abs(pos.x - reference.x)) {
                pos.x += width;
            }
            if (Math.Abs(pos.z - length - reference.z) < Math.Abs(pos.z - reference.z)) {
                pos.z -= length;
            }  else if (Math.Abs(pos.z + length - reference.z) < Math.Abs(pos.z - reference.z)) {
                pos.z += length;
            }
            return pos;
        }

        public bool IsInside(Vector3Int pos) {
            return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height && pos.z >= 0 && pos.z < length;
        }

        public bool Equals(WorldSettings other) {
            return width == other.width && height == other.height && length == other.length;
        }

        public override bool Equals(object obj) {
            return obj is WorldSettings other && Equals(other);
        }

        public override int GetHashCode() {
            return HashCode.Combine(width, height, length);
        }
    }
}