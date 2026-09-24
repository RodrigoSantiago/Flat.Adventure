using Game.Utils;
using UnityEngine;

namespace Game.GraphicGenerator {
    public static class GraphicsBufferAllocator {
        public static readonly MemoryAllocator<GraphicsBuffer> Allocator = new(
            size => new GraphicsBuffer(GraphicsBuffer.Target.Structured, size, 16), 
            128, 
            256 * 1024 * 1024 / 16);
        // public static readonly EasyAllocator Allocator = new();
    }

    public class EasyAllocator {

        public class Node : INode<GraphicsBuffer> {
            public GraphicsBuffer Source { get; }
            public int Index { get; }

            public Node(GraphicsBuffer source, int index) {
                Source = source;
                Index = index;
            }
        }
        
        public Allocation<GraphicsBuffer> Alloc(int elements) {
            var buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, elements, 16);
            return new Allocation<GraphicsBuffer>(new Node(buffer, 0), elements);
        }

        public void Free(Allocation<GraphicsBuffer> allocation) {
            allocation.Node.Source.Dispose();
        }

        public void Dispose() {
            
        }
    }
}