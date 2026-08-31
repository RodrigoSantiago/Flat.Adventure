using UnityEngine;

namespace Game.Data {
    public struct IndexPos {
        public int x;
        public int y;
        public int z;

        public IndexPos(int x, int y, int z) {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public IndexPos GetChunkIndex(int lod) {
            int chunkSize = 32 << lod;
            int mask = ~(chunkSize - 1);

            return new IndexPos(
                x & mask,
                y & mask,
                z & mask
            );
        }

        public override string ToString() {
            return x + ", " + y + ", " + z;
        }

        public override bool Equals(object obj) {
            return obj is IndexPos other && this == other;
        }

        public override int GetHashCode() {
            unchecked {
                int hash = 17;
                hash = hash * 31 + x;
                hash = hash * 31 + y;
                hash = hash * 31 + z;
                return hash;
            }
        }

        public static bool operator ==(IndexPos a, IndexPos b) {
            return a.x == b.x && a.y == b.y && a.z == b.z;
        }

        public static bool operator !=(IndexPos a, IndexPos b) {
            return a.x != b.x || a.y != b.y || a.z != b.z;
        }

        public static IndexPos operator +(IndexPos a, IndexPos b) {
            return new IndexPos(
                a.x + b.x,
                a.y + b.y,
                a.z + b.z
            );
        }

        public static IndexPos operator +(IndexPos a, int b) {
            return new IndexPos(
                a.x + b,
                a.y + b,
                a.z + b
            );
        }

        public static IndexPos operator +(int a, IndexPos b) {
            return new IndexPos(
                a + b.x,
                a + b.y,
                a + b.z
            );
        }

        public static IndexPos operator -(IndexPos a, IndexPos b) {
            return new IndexPos(
                a.x - b.x,
                a.y - b.y,
                a.z - b.z
            );
        }

        public static IndexPos operator -(IndexPos a, int b) {
            return new IndexPos(
                a.x - b,
                a.y - b,
                a.z - b
            );
        }

        public static IndexPos operator -(int a, IndexPos b) {
            return new IndexPos(
                a - b.x,
                a - b.y,
                a - b.z
            );
        }

        public static IndexPos operator *(IndexPos a, IndexPos b) {
            return new IndexPos(
                a.x * b.x,
                a.y * b.y,
                a.z * b.z
            );
        }

        public static IndexPos operator *(IndexPos a, int b) {
            return new IndexPos(
                a.x * b,
                a.y * b,
                a.z * b
            );
        }

        public static IndexPos operator *(int a, IndexPos b) {
            return new IndexPos(
                a * b.x,
                a * b.y,
                a * b.z
            );
        }

        public static IndexPos operator /(IndexPos a, IndexPos b) {
            return new IndexPos(
                a.x / b.x,
                a.y / b.y,
                a.z / b.z
            );
        }

        public static IndexPos operator /(IndexPos a, int b) {
            return new IndexPos(
                a.x / b,
                a.y / b,
                a.z / b
            );
        }

        public static IndexPos operator %(IndexPos a, IndexPos b) {
            return new IndexPos(
                a.x % b.x,
                a.y % b.y,
                a.z % b.z
            );
        }

        public static IndexPos operator %(IndexPos a, int b) {
            return new IndexPos(
                a.x % b,
                a.y % b,
                a.z % b
            );
        }

        public Vector3 ToVector3() {
            return new Vector3(x, y, z);
        }

        public static IndexPos FromVector3(Vector3 pos) {
            return new IndexPos(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
        }

        public static int FloorSnapTo(int number, int snap) {
            int mz = number % snap;
            return mz >= 0 ? number - mz : number - snap - mz;
        }
        
        public static int RoundSnapTo(int number, int snap) {
            int remainder = number % snap;
            if (remainder == 0) {
                return number;
            }
            if (remainder < 0) {
                remainder = snap + remainder;
            }
            if (remainder + remainder >= snap) {
                return number - remainder + snap;
            } else {
                return number - remainder;
            }
        }
        
        public IndexPos RoundSnapTo(int snap) {
            return new IndexPos(RoundSnapTo(x, snap), RoundSnapTo(y, snap), RoundSnapTo(z, snap));
        }
        
        public IndexPos SnapTo(int snap) {
            int mx = x % snap;
            int my = y % snap;
            int mz = z % snap;

            return new IndexPos(
                mx >= 0 ? x - mx : x - snap - mx,
                my >= 0 ? y - my : y - snap - my,
                mz >= 0 ? z - mz : z - snap - mz
            );
        }
    }
}