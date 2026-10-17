using System;

namespace Game.Worlds.Exceptions {
    public class CacheGpuFailed : Exception {
        public CacheGpuFailed() {
        }

        public CacheGpuFailed(string message) : base(message) {
        }

        public CacheGpuFailed(string message, Exception innerException) : base(message, innerException) {
        }
    }
}