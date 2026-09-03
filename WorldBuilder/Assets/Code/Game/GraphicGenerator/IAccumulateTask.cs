using System;

namespace Game.GraphicGenerator {
    public interface IAccumulateTask<T> where T : IAccumulateTask<T> {
        public int Count { get; }

        public bool IsReplaceable(T task);
        public int Similarity(T task);
        public void Group(T task);
    }
}