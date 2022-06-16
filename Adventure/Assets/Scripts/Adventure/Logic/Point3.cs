namespace Adventure.Logic {
    public struct Point3 {
        public int x;
        public int y;
        public int z;

        public Point3(int x, int y, int z) {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Point3 operator +(Point3 a, Point3 b) {
            return new Point3(a.x + b.x, a.y + b.y, a.z + b.z);
        }

        public static Point3 operator -(Point3 a, Point3 b) {
            return new Point3(a.x - b.x, a.y - b.y, a.z - b.z);
        }

        public static Point3 operator *(Point3 a, Point3 b) {
            return new Point3(a.x * b.x, a.y * b.y, a.z * b.z);
        }

        public static Point3 operator /(Point3 a, Point3 b) {
            return new Point3(a.x / b.x, a.y / b.y, a.z / b.z);
        }

        public static Point3 operator %(Point3 a, Point3 b) {
            return new Point3(a.x % b.x, a.y % b.y, a.z % b.z);
        }

        public static Point3 operator +(Point3 a, int b) {
            return new Point3(a.x + b, a.y + b, a.z + b);
        }

        public static Point3 operator -(Point3 a, int b) {
            return new Point3(a.x - b, a.y - b, a.z - b);
        }

        public static Point3 operator *(Point3 a, int b) {
            return new Point3(a.x * b, a.y * b, a.z * b);
        }

        public static Point3 operator /(Point3 a, int b) {
            return new Point3(a.x / b, a.y / b, a.z / b);
        }

        public static Point3 operator %(Point3 a, int b) {
            return new Point3(a.x % b, a.y % b, a.z % b);
        }

        public static Point3 operator +(int b, Point3 a) {
            return new Point3(a.x + b, a.y + b, a.z + b);
        }

        public static Point3 operator -(int b, Point3 a) {
            return new Point3(a.x - b, a.y - b, a.z - b);
        }

        public static Point3 operator *(int b, Point3 a) {
            return new Point3(a.x * b, a.y * b, a.z * b);
        }

        public static Point3 operator /(int b, Point3 a) {
            return new Point3(a.x / b, a.y / b, a.z / b);
        }

        public static Point3 operator %(int b, Point3 a) {
            return new Point3(a.x % b, a.y % b, a.z % b);
        }
    }
}