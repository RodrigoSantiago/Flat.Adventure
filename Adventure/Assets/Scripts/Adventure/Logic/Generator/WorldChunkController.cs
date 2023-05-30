using System.Collections.Generic;
using Adventure.Logic.Data;
using UnityEngine;

namespace Adventure.Logic.Generator {
    public class WorldChunkController {
        private const int MAX_LOD = 4;
        
        private readonly World world;
        private readonly WorldGenerator generator;
        
        private readonly Queue<ChunkRequest> readyRequests = new();
        private readonly Dictionary<Vector3Int, ChunkRequest>[] requestLod = new Dictionary<Vector3Int, ChunkRequest>[MAX_LOD + 1];
        private readonly Dictionary<Vector3Int, Chunk>[] chunks = new Dictionary<Vector3Int, Chunk>[MAX_LOD + 1];

        public WorldChunkController(World world, WorldGenerator generator) {
            this.world = world;
            this.generator = generator;
            
            for (int i = 0; i < chunks.Length; i++) {
                chunks[i] = new Dictionary<Vector3Int, Chunk>();
            }
            for (int i = 0; i < requestLod.Length; i++) {
                requestLod[i] = new Dictionary<Vector3Int, ChunkRequest>();
            }
        }

        public void LoadChunkSchedule() {
            var generatedChunk = generator.Read();
            while (generatedChunk != null) {
                var request = requestLod[generatedChunk.lod][generatedChunk.local];
                requestLod[generatedChunk.lod].Remove(generatedChunk.local);
                
                readyRequests.Enqueue(request);
                generatedChunk = generator.Read();
            } 
            
            while (readyRequests.Count > 0) {
                var readyRequest = readyRequests.Dequeue();
                chunks[readyRequest.lod][readyRequest.local] = readyRequest.chunk;
                
                if (readyRequest.listeners != null) {
                    foreach (var listener in readyRequest.listeners) {
                        listener.OnChunkReceived(readyRequest.chunk);
                    }
                }
                
                if (readyRequest.lod < MAX_LOD) {
                    ConsumeLod(readyRequest.chunk, readyRequest.lod + 1);
                }
            }
        }

        public Chunk GetChunk(Vector3Int local, int lod = 0) {
            chunks[lod].TryGetValue(local, out var chunk);
            return chunk;
        }

        public bool IsChunkReady(Vector3Int local, int lod = 0) {
            return chunks[lod].ContainsKey(local);
        }

        private void ConsumeLod(Chunk chunk, int lod) {
            Vector3Int localLod = chunk.local / (16 * (1 << lod)) * (16 * (1 << lod));
            
            if (requestLod[lod].TryGetValue(localLod, out var request)) {
                request.AddChunk(chunk);
                if (request.IsReady()) {
                    generator.AddChunkRequestPriority(request);
                }
            }
        }

        public void RequestChunk(Vector3Int local, WorldPlayerController source = null, int lod = 0) {
            local = world.settings.Pos(local);
            
            if (local / (16 * (1 << lod)) * (16 * (1 << lod)) != local) {
                Debug.Log("Invalid request " + local+" >> "+lod);
                return;
            }
            
            if (chunks[lod].TryGetValue(local, out var chunkLod)) {
                source?.OnChunkReceived(chunkLod);
                
            } else if (requestLod[lod].TryGetValue(local, out var chunkRequest)) {
                chunkRequest.AddListener(source);
                
            } else {
                chunkRequest = new ChunkRequest(local, lod);
                chunkRequest.AddListener(source);
                if (lod > 0) {
                    for (int z = 0; z < 2; z++)
                    for (int y = 0; y < 2; y++)
                    for (int x = 0; x < 2; x++) {
                        Vector3Int localLod = local + new Vector3Int(x, y, z) * (16 * (1 << (lod - 1)));
                        if (chunks[lod - 1].TryGetValue(localLod, out var chunkPiece)) {
                            chunkRequest.AddChunk(chunkPiece);
                        } else {
                            RequestChunk(localLod, null, lod - 1);
                        }
                    }

                    requestLod[lod][local] = chunkRequest;
                    if (chunkRequest.IsReady()) {
                        generator.AddChunkRequest(chunkRequest);
                    }
                } else {
                    requestLod[lod][local] = chunkRequest;
                    generator.AddChunkRequest(chunkRequest);
                }
            }
        }

        public Chunk LoadChunk(Vector3Int local) {
            if (chunks[0].TryGetValue(local, out var chunk)) {
                return chunk;
            }

            chunk = world.worldMap.GenerateChunk(local);
            chunks[0].Add(local, chunk);

            return chunk;
        }

        private void UnloadChunk(Chunk chunk) {
            if (!chunk.original) {
                // save to file
            }
            
            chunks[0].Remove(chunk.local);
        }
    }
}