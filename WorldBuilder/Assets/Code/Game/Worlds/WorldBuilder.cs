using System;
using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Worlds.Exceptions;
using Game.Worlds.Generation;
using Game.Worlds.Storage;
using UnityEngine;
using WorldCache = Game.Worlds.CacheManagement.WorldCache;

namespace Game.Worlds {
    
    public class WorldBuilder {
        
        public Action<Chunk> OnChunkLoaded { get; set; }
        public Action<IndexPos, int, Exception> OnChunkCorrupted { get; set; }

        private readonly Dictionary<IndexLodPos, RegionRequest> requests = new();
        
        private WorldManager Manager { get; }
        private WorldCache Cache => Manager.Cache;
        private WorldGenerator Generator => Manager.Generator;

        public WorldBuilder(WorldManager manager) {
            Manager = manager;
        }

        class RegionRequest {
            public readonly int lod;
            public readonly IndexPos regionIndex;
            public readonly HashSet<IndexPos> requiredChunks = new();

            public RegionRequest(int lod, IndexPos regionIndex) {
                this.lod = lod;
                this.regionIndex = regionIndex;
            }

            public void Add(IndexPos chunkIndex) {
                requiredChunks.Add(chunkIndex);
            }

            public void Remove(IndexPos chunkIndex) {
                requiredChunks.Remove(chunkIndex);
            }

            public bool IsEmpty => requiredChunks.Count == 0;
        }
        
        private bool cancelled;
        
        public void Cancel() {
            cancelled = true;
            requests.Clear();
        }

        private void OnRequestDone(RegionRequest request, bool allowNewRequest) {
            if (cancelled) return;
            
            requests.Remove(new IndexLodPos(request.regionIndex, request.lod));
            
            request.requiredChunks.RemoveWhere(chunkIndex => {
                var chunk = Cache.LoadChunkCache(chunkIndex, request.lod);
                if (chunk?.CurrentVersion > 0) {
                    OnChunkLoaded(chunk);
                    return true;
                }
                return false;
            });
            
            if (!request.IsEmpty) {
                if (allowNewRequest) {
                    RequestUnknown(request.requiredChunks.First(), request.lod, request.requiredChunks);
                } else {
                    // Unexpected 
                    Debug.Log("Unexpected");
                }
            }
        }

        private void OnRequestFail(RegionRequest request, Exception error, bool allowNewRequest) {
            if (cancelled) return;
            
            requests.Remove(new IndexLodPos(request.regionIndex, request.lod));
            if (allowNewRequest) {
                RequestUnknown(request.requiredChunks.First(), request.lod, request.requiredChunks);
            } else {
                foreach (var index in request.requiredChunks) {
                    OnChunkCorrupted(index, request.lod, error);
                }
            }
        }

        public void RequestChunk(IndexPos chunkIndex, int lod) {
            if (cancelled) return;
            
            var regionIndex = chunkIndex.GetChunkIndex(Region.MaxLod);
            var chunkPos = new IndexLodPos(chunkIndex, lod);
            var regionPos = new IndexLodPos(regionIndex, lod);

            var chunk = Cache.LoadChunkCache(chunkIndex, lod);
            if (chunk != null) {
                if (chunk.CurrentVersion > 0) {
                    OnChunkLoaded(chunk);
                } else {
                    RequestUnknown(chunkIndex, lod);
                }
            } else {
                if (!requests.TryGetValue(regionPos, out var request)) {
                    request = new RegionRequest(lod, regionIndex);
                    requests[regionPos] = request;

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
                                Debug.Log("CacheCorrupted");
                                GameManager.Instance.RunSync(() => OnRequestFail(request, error, false));
                            } else {
                                Debug.Log("Error");
                                GameManager.Instance.RunSync(() => OnRequestFail(request, error, false));
                            }
                        });
                }
                
                request.Add(chunkIndex);
            }
        }

        private void RequestUnknown(IndexPos chunkIndex, int lod, IEnumerable<IndexPos> requiredChunks = null) {
            var regionIndex = chunkIndex.GetChunkIndex(Region.MaxLod);
            var regionPos = new IndexLodPos(regionIndex, lod);
            
            if (!requests.TryGetValue(regionPos, out var request)) {
                request = new RegionRequest(lod, regionIndex);
                requests[regionPos] = request;

                // If network => request.requiredChunks
                Generator.EnqueueRegion(regionIndex, () => {
                    GameManager.Instance.RunSync(() => OnRequestDone(request, false));
                });
            }

            if (requiredChunks != null) {
                foreach (var index in requiredChunks) {
                    request.Add(index);
                }
            }
        }
        
        public void ReleaseChunk(IndexPos chunkIndex, int lod) {
            Cache.ReleaseChunkCache(chunkIndex, lod);
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