using System;
using System.Collections.Generic;
using Game.Data;
using Game.Worlds.Generation;
using Game.Worlds.Storage;

namespace Game.Worlds {
    
    public class WorldBuilder {
        
        public Action<Chunk> OnChunkLoaded { get; set; }
        public Action<IndexPos, int, Exception> OnChunkCorrupted { get; set; }

        private readonly Dictionary<(int lod, IndexPos pos), RegionRequest> requests = new();
        
        private WorldManager Manager { get; }
        private WorldCache Cache => Manager.Cache;
        private WorldGenerator Generator => Manager.Generator;

        public WorldBuilder(WorldManager manager) {
            Manager = manager;
        }

        class RegionRequest {
            public readonly int lod;
            public readonly IndexPos regionIndex;
            public readonly List<IndexPos> requiredChunks = new();

            public RegionRequest(int lod, IndexPos regionIndex) {
                this.lod = lod;
                this.regionIndex = regionIndex;
            }
        }

        private void OnRequestDone(RegionRequest request, bool allowNewRequest) {
            for (int i = 0; i < request.requiredChunks.Count; i++) {
                var chunkIndex = request.requiredChunks[i];
                
                var chunk = Cache.LoadChunkCache(chunkIndex, request.lod);
                if (chunk?.CurrentVersion > 0) {
                    request.requiredChunks.RemoveAt(i--);
                    OnChunkLoaded(chunk);
                }
            }

            requests.Remove((request.lod, request.regionIndex));
            
            if (request.requiredChunks.Count > 0) {
                if (allowNewRequest) {
                    RequestUnknown(request.regionIndex, request.lod, request.requiredChunks);
                } else {
                    // Unexpected 
                }
            }
        }

        public void RequestChunk(IndexPos chunkIndex, int lod) {
            var regionIndex = chunkIndex.GetChunkIndex(Region.MaxLod);

            var chunk = Cache.LoadChunkCache(chunkIndex, lod);
            if (chunk != null) {
                if (chunk.CurrentVersion > 0) {
                    OnChunkLoaded(chunk);
                } else {
                    RequestUnknown(regionIndex, lod, new List<IndexPos> { chunkIndex });
                }
            } else {
                if (!requests.TryGetValue((lod, regionIndex), out var request)) {
                    request = new RegionRequest(lod, regionIndex);
                    request.requiredChunks.Add(chunkIndex);
                    requests[(lod, regionIndex)] = request;

                    Cache.RequestLoadRegion(regionIndex, lod,
                        () => {
                            GameManager.Instance.RunSync(() => OnRequestDone(request, true));
                        },
                        (error) => {
                            if (error is CacheNotFound) {
                                GameManager.Instance.RunSync(() => OnRequestDone(request, true));
                            } else if (error is CacheCorrupted) {
                                // If network => request
                                // If local => Show Error, Avoid showing multiple errors modals at the same time
                                GameManager.Instance.RunSync(() => OnChunkCorrupted(regionIndex, lod, error));
                            }
                        });
                } else {
                    request.requiredChunks.Add(chunkIndex);
                }
            }
        }

        private void RequestUnknown(IndexPos regionIndex, int lod, List<IndexPos> requiredChunks) {
            var request = new RegionRequest(lod, regionIndex);
            request.requiredChunks.AddRange(requiredChunks);
            
            // If network => request.requiredChunks
            Generator.EnqueueRegion(regionIndex, () => {
                GameManager.Instance.RunSync(() => OnRequestDone(request, false));
            });
        }
        
        public void ReleaseChunk(Chunk chunk) {
            Cache.ReleaseChunkCache(chunk);
        }
        
        // ---------------------------------
        //      Request
        // ---------------------------------
        // RequestChunk
        // RequestTerrainModify
        
        // -- Structure, Prop, Unit, Item, Tree, Grass
        // Request[Unit]Create
        // Request[Unit]Modify
        // Request[Unit]Remove
        
        // ---------------------------------
        //      Result
        // ---------------------------------
        // OnChunkLoaded
        // OnTerrainModified
        
        // -- Structure, Prop, Unit, Item, Tree, Grass
        // On[Unit]Created
        // On[Unit]Modified
        // On[Unit]Removed
    }
}