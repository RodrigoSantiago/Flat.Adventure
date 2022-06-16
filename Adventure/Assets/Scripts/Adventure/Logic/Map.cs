namespace Adventure.Logic {
    public class Map {

        public int level;
        public float[] geology;        // 0 - Depression | 1 - Hill
        public float[] moisture;       // 0 - Dry        | 1 - Wet
        public float[] temperature;    // 0 - Cold       | 1 - Hot
    }
}