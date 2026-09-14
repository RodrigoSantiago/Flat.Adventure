using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Data;
using Game.Data.Queues;
using Game.Worlds.Exceptions;

namespace Game.Worlds.CacheManagement {
    public class CacheWorkWrite : CacheWork {
        private readonly ChunkCachePair[] pairs;
        
        public override bool IsWrite => true;
        
        public CacheWorkWrite(WorldCache cache, IndexPos regionIndex, ChunkCachePair[] pairs, TaskListener listener) 
            : base(cache, regionIndex, listener) {
            this.pairs = pairs;
        }

        public override bool Group(CacheWork task) {
            return false;
        }

        public override void Execute() {
            string path = Cache.Directory + "/" + Region.GenName(RegionIndex) + ".region";
            bool exists = File.Exists(path);

            foreach (var pair in pairs) {
                pair.Chunk.ExportData(pair.Update);
            }

            var updates = pairs.Select(pair => pair.Update).ToArray();
            
            Cache.Serializer.Save(path, updates, exists);
            
            Cache.UpdateRegion(RegionIndex, updates);
        }
    }

    public class CacheWorkRead : CacheWork {
        protected int requiredLods;
        
        public override bool IsRead => true;

        public CacheWorkRead(WorldCache cache, IndexPos regionIndex, int requiredLods, TaskListener listener) 
            : base(cache, regionIndex, listener) {
            
            this.requiredLods = requiredLods;
            Attempts = 2;
        }
        
        public override bool Group(CacheWork task) {
            if (task is CacheWorkRead read && read.RegionIndex == RegionIndex) {
                requiredLods |= read.requiredLods;
                listeners.AddRange(read.listeners);
                return true;
            }

            return false;
        }

        public override void Execute() {
            string path = Cache.Directory + "/" + Region.GenName(RegionIndex) + ".region";
            if (!File.Exists(path)) {
                Cancel();
                
                throw new CacheNotFound();
            }
            
            var cacheChunks = Cache.Serializer.Load(path, requiredLods);
            
            var chunks = new List<Chunk>();
            foreach (var cacheChunk in cacheChunks) {
                int id = cacheChunk.chunkEntryId;
                int lod = Region.GetLod(id);
                int local = Region.GetLocalGroupId(id);
                var localPos = Region.GetLocalPosition(lod, local);
                chunks.Add(new Chunk(RegionIndex + localPos, lod, cacheChunk));
            }
            
            Cache.PutRegion(RegionIndex, chunks);
        }
    }

    public abstract class CacheWork : ITaskGroup<CacheWork> {
        protected WorldCache Cache { get; }

        public int Attempts { get; set; }

        public IndexPos RegionIndex { get; }
        protected readonly List<TaskListener> listeners;

        public virtual bool IsRead => false;
        public virtual bool IsWrite => false;
        
        private int compare;

        protected CacheWork(WorldCache cache, IndexPos regionIndex, TaskListener listener) {
            Cache = cache;
            RegionIndex = regionIndex;
            listeners = new List<TaskListener> { listener };
        }

        public void SetSortValue(IndexPos sort) {
            var d = RegionIndex - sort;
            compare = d.x * d.x + d.y * d.y + d.z * d.z;
        }

        public int Compare() {
            return compare;
        }

        public abstract bool Group(CacheWork task);

        public abstract void Execute();

        public void SetSuccess() {
            foreach (var listener in listeners) {
                listener.OnSuccess();
            }
        }

        public void SetError(Exception message) {
            foreach (var listener in listeners) {
                listener.OnError(message);
            }
        }

        public void Cancel() {
            Attempts = 0;
        }
    }
}