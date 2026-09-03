using System;
using System.Collections.Generic;
using Game.Data;
using UnityEngine;

namespace Game.GraphicGenerator {
    public class ChunkRenderTask : IAccumulateTask<ChunkRenderTask> {

        public int Lod { get; private set; }
        public IndexPos Pos { get; private set; }
        public Chunk[] Chunks { get; private set; }
        public Action<Mesh> Action { get; private set; }

        public int Count => tasks.Count;
        
        public List<ChunkRenderTask> tasks = new();

        public ChunkRenderTask(int lod, IndexPos pos, Chunk[] chunks, Action<Mesh> action) {
            Lod = lod;
            Pos = pos;
            Chunks = chunks;
            Action = action;
            tasks.Add(this);
        }

        public bool IsReplaceable(ChunkRenderTask task) {
            if (task.Pos == Pos && task.Lod == Lod) {
                return true;
            }

            return false;
        }

        public int Similarity(ChunkRenderTask task) {
            if (Lod != task.Lod) return 0;

            int chunkSize = 32 << Lod;

            int dx = Math.Abs(Pos.x - task.Pos.x) / chunkSize;
            int dy = Math.Abs(Pos.y - task.Pos.y) / chunkSize;
            int dz = Math.Abs(Pos.z - task.Pos.z) / chunkSize;

            if (dx >= 3 || dy >= 3 || dz >= 3) return 0;

            int overlapX = 3 - dx;
            int overlapY = 3 - dy;
            int overlapZ = 3 - dz;

            return overlapX * overlapY * overlapZ;
        }

        public void Group(ChunkRenderTask task) {
            tasks.AddRange(task.tasks);
        }
    }
}