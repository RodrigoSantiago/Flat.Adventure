using System;

namespace Game.Data {
    public struct IndexLodPos : IEquatable<IndexLodPos> {
        public IndexPos pos;
        public int lod;

        public IndexLodPos(int x, int y, int z, int lod) {
            this.pos = new IndexPos(x, y, z);
            this.lod = lod;
        }

        public IndexLodPos(IndexPos pos, int lod) {
            this.pos = pos;
            this.lod = lod;
        }

        public bool Equals(IndexLodPos other) {
            return pos == other.pos && lod == other.lod;
        }

        public override bool Equals(object obj) {
            return obj is IndexLodPos other && Equals(other);
        }

        public static bool operator ==(IndexLodPos a, IndexLodPos b) {
            return a.lod == b.lod && a.pos == b.pos;
        }

        public static bool operator !=(IndexLodPos a, IndexLodPos b) {
            return a.lod != b.lod || a.pos != b.pos;
        }

        public override int GetHashCode() {
            return HashCode.Combine(pos, lod);
        }

        public override string ToString() {
            return pos + "[" + lod + "]";
        }
    }
}