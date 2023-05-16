using System;
using System.Collections.Generic;
using Adventure.Logic.Data;
using UnityEngine;

namespace Adventure.Logic {
    public class WorldChunkController {

        private readonly World world;
        private readonly Queue<ChunkRequest> requests = new();
        private readonly Dictionary<Vector3Int, Chunk> chunks = new();

        public WorldChunkController(World world) {
            this.world = world;
        }

        public void LoadChunkSchedule() {
            long time = WorldTime.currentTimeMillis;
            while (Consume()) {
                if (WorldTime.currentTimeMillis - time > 12) {
                    break;
                }
            }
        }

        private bool Consume() {
            if (requests.Count == 0) {
                return false;
            }
            
            var request = requests.Dequeue();
            var chunk = GetChunk(request.local);
            if (request.source != null) {
                request.source.OnChunkReceived(chunk);
            }

            return true;
        }

        public bool IsChunkReady(Vector3Int local) {
            return chunks.ContainsKey(local);
        }

        public bool IsChunkForReady(Vector3Int local) {
            if (IsChunkReady(local)) {
                return true;
            }

            foreach (var request in requests) {
                if (request.local == local) {
                    return true;
                }
            }

            return false;
        }

        public void RequestChunk(Vector3Int local, WorldPlayerController source = null) {
            var request = new ChunkRequest(local, source);
            if (!requests.Contains(request)) {
                requests.Enqueue(request);
            }
        }

        public Chunk GetChunk(Vector3Int local) {
            if (chunks.TryGetValue(local, out var chunk)) {
                return chunk;
            }

            chunk = world.worldMap.GenerateChunk(local);
            chunks.Add(local, chunk);

            return chunk;
        }

        private void UnloadChunk(Chunk chunk) {
            if (!chunk.original) {
                // save to file
            }
            
            chunks.Remove(chunk.local);
        }
        
        public readonly struct ChunkRequest {
            public readonly WorldPlayerController source;
            public readonly Vector3Int local;

            public ChunkRequest(Vector3Int local, WorldPlayerController source) {
                this.local = local;
                this.source = source;
            }

            public bool Equals(ChunkRequest other) {
                return Equals(source, other.source) && local.Equals(other.local);
            }

            public override bool Equals(object obj) {
                return obj is ChunkRequest other && Equals(other);
            }

            public override int GetHashCode() {
                return HashCode.Combine(source, local);
            }
        }
    }
}