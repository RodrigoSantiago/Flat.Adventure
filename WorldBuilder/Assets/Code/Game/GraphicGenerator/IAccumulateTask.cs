using Game.Data;

namespace Game.GraphicGenerator {
    public interface IAccumulateTask<T> where T : IAccumulateTask<T> {
        public int Count { get; }

        public void SetSortValue(IndexPos sort);
        public int Compare();
        
        public bool IsReplaceable(T task);
        public int Similarity(T task);
        public void Group(T task);
    }
}