using System;

namespace Adventure.Logic {
    public static class WorldTime {
        public static long currentTimeMillis {
            get {
                return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
        }

        public static float deltaTime {
            get; internal set;
        }
    }
}