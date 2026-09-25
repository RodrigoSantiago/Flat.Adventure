using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Game.Utils {

    public interface INode<T> where T : class, IDisposable {
        public T Source { get; }
        public int Index { get; }
    }

    public class Allocation<T> where T : class, IDisposable {
        public T Source => Node?.Source;
        public int Index => Node?.Index ?? 0;
        public int Count { get; }
        public INode<T> Node { get; }

        public Allocation(INode<T> node, int count) {
            Node = node;
            Count = count;
        }
    }

    public sealed class MemoryAllocator<T> : IDisposable where T : class, IDisposable {

        class Node : INode<T> {
            public T Source => bucket.source;
            public int Index => index;

            public readonly MemoryAllocator<T> owner;
            public readonly Bucket bucket;
            public readonly int index;
            public int count;
            public bool free;
        
            public Node nextInBin;
            public Node prevInBin;
        
            public Node nextInBucket;
            public Node prevInBucket;

            public Node(MemoryAllocator<T> owner, Bucket bucket, int index, int count) {
                this.owner = owner;
                this.bucket = bucket;
                this.index = index;
                this.count = count;
            }
        }

        class Bucket {
            public readonly T source;
            public int freeSpace;
            public Node head;

            public Bucket(T source, int freeSpace) {
                this.source = source;
                this.freeSpace = freeSpace;
            }
        }
        
        private const int MaxBinCount = 32;
        
        private readonly int minCapacity;
        private readonly int maxCapacity;
        private readonly Func<int, T> factory;
        
        private readonly List<Bucket> buckets = new ();
        private readonly Node[] bins = new Node[MaxBinCount];
        private bool dispose;
        
        public MemoryAllocator(Func<int, T> factory, int minCapacity, int maxCapacity) {
            if (minCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(minCapacity));

            if (maxCapacity < minCapacity)
                throw new ArgumentOutOfRangeException(nameof(maxCapacity));
            
            if (maxCapacity % minCapacity != 0)
                throw new ArgumentException("maxCapacity must be a multiple of minCapacity.");
            
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            this.minCapacity = minCapacity;
            this.maxCapacity = maxCapacity;
        }

        public Allocation<T> Alloc(int elements) {
            if (elements <= 0 || elements > maxCapacity || dispose) return default;

            int alignedSize = GetAlignedCount(elements);
            bool p2 = (alignedSize & (alignedSize - 1)) == 0;
            int binIndex = Math.Min(MaxBinCount - 1, GetBinIndex(alignedSize) + (p2 ? 0 : 1));
            
            while (binIndex < MaxBinCount - 1 && bins[binIndex] == null) {
                binIndex++;
            }

            var node = bins[binIndex];
            if (node == null) {
                Bucket bucket;
                try {
                    bucket = new Bucket(factory.Invoke(maxCapacity), maxCapacity);
                } catch {
                    return default;
                }

                buckets.Add(bucket);
                
                node = new Node(this, bucket, 0, maxCapacity);
                bucket.head = node;
                node.free = true;
                AddNodeToBin(node);
            }
            
            node.free = false;
            RemoveNodeFromBin(node);
            
            if (node.count - alignedSize >= minCapacity) {
                SplitNode(node, alignedSize);
            }

            node.bucket.freeSpace -= node.count;
            return new Allocation<T>(node, elements);
        }

        public void Free(Allocation<T> allocation) {
            if (dispose || allocation.Node is not Node node || node.owner != this || node.free) return;
            
            node.free = true;
            node.bucket.freeSpace += node.count;
            
            var next = node.nextInBucket?.free == true ? node.nextInBucket : null;
            var prev = node.prevInBucket?.free == true ? node.prevInBucket : null;
            
            if (prev != null && next != null) {
                prev.nextInBucket = next.nextInBucket;
                if (prev.nextInBucket != null) {
                    prev.nextInBucket.prevInBucket = prev;
                }

                RemoveNodeFromBin(next);
                RemoveNodeFromBin(prev);
                
                prev.count += node.count + next.count;
                AddNodeToBin(prev);
            } else if (prev != null) {
                prev.nextInBucket = node.nextInBucket;
                if (prev.nextInBucket != null) {
                    prev.nextInBucket.prevInBucket = prev;
                }

                RemoveNodeFromBin(prev);
                
                prev.count += node.count;
                AddNodeToBin(prev);
            } else if (next != null) {
                node.nextInBucket = next.nextInBucket;
                if (node.nextInBucket != null) {
                    node.nextInBucket.prevInBucket = node;
                }

                RemoveNodeFromBin(next);
                
                node.count += next.count;
                AddNodeToBin(node);
            } else {
                
                AddNodeToBin(node);
            }
        }

        private void SplitNode(Node node, int size) {
            var split = new Node(this, node.bucket, node.index + size, node.count - size);
            node.count = size;

            split.prevInBucket = node;
            split.nextInBucket = node.nextInBucket;
            if (split.nextInBucket != null) {
                split.nextInBucket.prevInBucket = split;
            }
            node.nextInBucket = split;

            split.free = true;
            AddNodeToBin(split);
        }

        private void AddNodeToBin(Node node) {
            int binIndex = GetBinIndex(node.count);

            var head = bins[binIndex];

            node.prevInBin = null;
            node.nextInBin = head;

            if (head != null) {
                head.prevInBin = node;
            }

            bins[binIndex] = node;
        }

        private void RemoveNodeFromBin(Node node) {
            int binIndex = GetBinIndex(node.count);
            if (bins[binIndex] == node) {
                bins[binIndex] = node.nextInBin;
            }
            var next = node.nextInBin;
            var prev = node.prevInBin;
            if (prev != null) {
                prev.nextInBin = next;
            }
            if (next != null) {
                next.prevInBin = prev;
            }
            
            node.nextInBin = null;
            node.prevInBin = null;
        }

        public void ReleaseUnusedBuckets() {
            for (int i = 0; i < buckets.Count; i++) {
                var bucket = buckets[i];
                if (bucket.freeSpace == maxCapacity) {
                    var head = bucket.head;
                    while (head != null) {
                        RemoveNodeFromBin(head);
                        head = head.nextInBucket;
                    }
                    bucket.source.Dispose();
                    buckets.RemoveAt(i--);
                }
            }
        }

        public void Dispose() {
            foreach (var bucket in buckets) {
                bucket.source.Dispose();
            }

            dispose = true;
        }

        private int GetAlignedCount(int elements) {
            int v = elements / minCapacity * minCapacity;
            return v < elements ? v + minCapacity : v;
        }

        private int GetBinIndex(int size) {
            return math.min(MaxBinCount - 1, 31 - math.lzcnt((uint)size));
        }
    }
}