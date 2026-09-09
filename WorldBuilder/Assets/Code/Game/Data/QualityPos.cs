using System;

namespace Game.Data {
    public struct QualityPos : IEquatable<QualityPos> {
        public IndexPos pos;
        public int lod;

        public QualityPos(int x, int y, int z, int lod) {
            this.pos = new IndexPos(x, y, z);
            this.lod = lod;
        }

        public QualityPos(IndexPos pos, int lod) {
            this.pos = pos;
            this.lod = lod;
        }

        public bool Equals(QualityPos other) {
            return pos == other.pos && lod == other.lod;
        }

        public override bool Equals(object obj) {
            return obj is QualityPos other && Equals(other);
        }

        public static bool operator ==(QualityPos a, QualityPos b) {
            return a.lod == b.lod && a.pos == b.pos;
        }

        public static bool operator !=(QualityPos a, QualityPos b) {
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