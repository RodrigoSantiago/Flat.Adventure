using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GpuBufferAllocator : IDisposable {
    private const long UInt4Bytes = 16;
    private const long InitialCapacityBytes = 128L * 1024 * 1024;
    private const long GrowthStepBytes = 512L * 1024 * 1024;

    private const uint ThreadsPerGroup = 256;
    private const uint UInt4PerThread = 4;
    private const uint MaxDispatchGroups = 65535;

    private readonly GpuBufferCopier copier;
    private readonly ComputeShader defragmentShader;
    private readonly int defragmentKernel;

    private readonly List<FreeBlock> freeBlocks = new();
    private readonly List<Allocation> allocations = new();

    private static readonly int Src = Shader.PropertyToID("Src");
    private static readonly int Dst = Shader.PropertyToID("Dst");
    private static readonly int Moves = Shader.PropertyToID("Moves");
    private static readonly int MoveCount = Shader.PropertyToID("moveCount");
    private static readonly int TotalLength = Shader.PropertyToID("totalLength");
    private static readonly int GroupsX = Shader.PropertyToID("groupsX");

    private GraphicsBuffer buffer;
    private long capacity;

    public GraphicsBuffer Buffer => buffer;
    public long Capacity => capacity;
    public long Used => capacity - Free;
    public long Free => GetFreeMemory();
    public long AllocationCount => allocations.Count;

    public long LargestFreeBlock {
        get {
            if (freeBlocks.Count == 0)
                return 0;

            long largest = 0;

            for (int i = 0; i < freeBlocks.Count; i++) {
                if (freeBlocks[i].Length > largest)
                    largest = freeBlocks[i].Length;
            }

            return largest;
        }
    }

    public double LargestFreeBlockRatio {
        get {
            long totalFree = Free;

            if (totalFree == 0)
                return 0;

            return (double)LargestFreeBlock / totalFree;
        }
    }

    public double Fragmentation {
        get {
            long totalFree = Free;

            if (totalFree == 0)
                return 0;

            return 1.0 - (double)LargestFreeBlock / totalFree;
        }
    }

    public GpuBufferAllocator(GpuBufferCopier copier) {
        this.copier = copier ?? throw new ArgumentNullException(nameof(copier));

        var shader = Resources.Load<ComputeShader>("GpuBufferDefragment");

        defragmentShader = shader
            ? shader
            : throw new InvalidOperationException(
                "Failed to load 'GpuBufferDefragment' from Resources."
            );

        defragmentKernel = defragmentShader.FindKernel("DefragmentBuffer");

        capacity = BytesToUInt4(InitialCapacityBytes);
        buffer = CreateBuffer(capacity);

        freeBlocks.Add(new FreeBlock(0, capacity));
    }

    public Allocation Alloc(long length) {
        if (length <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(length),
                length,
                "Allocation length must be greater than zero."
            );

        FreeBlock? block = FindBestFit(length);

        if (block == null) {
            Grow(length);
            block = FindBestFit(length);

            if (block == null)
                throw new InvalidOperationException(
                    "Failed to allocate memory after growing the buffer."
                );
        }

        long offset = block.Value.Offset;

        RemoveFreeBlock(block.Value);

        long remaining = block.Value.Length - length;

        if (remaining > 0)
            InsertFreeBlock(
                new FreeBlock(offset + length, remaining)
            );

        var allocation = new Allocation(
            this,
            buffer,
            offset,
            length
        );

        allocations.Add(allocation);

        return allocation;
    }

    public void Dealloc(Allocation allocation) {
        if (allocation == null)
            throw new ArgumentNullException(nameof(allocation));

        if (allocation.Owner != this)
            throw new ArgumentException(
                "The allocation does not belong to this allocator.",
                nameof(allocation)
            );

        if (allocation.IsFreed)
            throw new InvalidOperationException(
                "The allocation has already been freed."
            );

        allocation.IsFreed = true;
        allocations.Remove(allocation);

        InsertFreeBlock(
            new FreeBlock(
                allocation.Offset,
                allocation.Length
            )
        );
    }

    public void Defragment() {
        if (allocations.Count == 0) {
            freeBlocks.Clear();
            freeBlocks.Add(new FreeBlock(0, capacity));
            return;
        }

        var sortedAllocations = GetSortedAllocations();

        long newOffset = 0;
        bool alreadyCompact = true;

        for (int i = 0; i < sortedAllocations.Count; i++) {
            Allocation allocation = sortedAllocations[i];

            if (allocation.Offset != newOffset) {
                alreadyCompact = false;
                break;
            }

            newOffset = checked(newOffset + allocation.Length);
        }

        if (alreadyCompact) {
            RebuildFreeList(sortedAllocations);
            return;
        }

        DefragmentToCapacity(capacity, sortedAllocations);
    }

    private void Grow(long requiredLength) {
        long requiredCapacity = checked(capacity + requiredLength);
        long newCapacity = CalculateGrowthCapacity(requiredCapacity);

        GraphicsBuffer newBuffer = CreateBuffer(newCapacity);

        try {
            copier.Copy(buffer, newBuffer, 0, 0, capacity, false);

            GraphicsBuffer oldBuffer = buffer;

            buffer = newBuffer;
            capacity = newCapacity;

            for (int i = 0; i < allocations.Count; i++)
                allocations[i].Buffer = buffer;

            oldBuffer.Release();

            RebuildFreeList();
        }
        catch {
            newBuffer.Release();
            throw;
        }
    }

    private void DefragmentToCapacity(
        long newCapacity,
        List<Allocation> sortedAllocations
    ) {
        GraphicsBuffer newBuffer = CreateBuffer(newCapacity);
        GraphicsBuffer moveBuffer = null;

        try {
            var moves = new Move[sortedAllocations.Count];

            long newOffset = 0;

            for (int i = 0; i < sortedAllocations.Count; i++) {
                Allocation allocation = sortedAllocations[i];

                moves[i] = new Move (
                    checked((uint)allocation.Offset),
                    checked((uint)newOffset),
                    checked((uint)allocation.Length),
                    checked((uint)(newOffset + allocation.Length))
                );

                newOffset = checked(
                    newOffset + allocation.Length
                );
            }

            moveBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                moves.Length,
                16
            );

            moveBuffer.SetData(moves);

            DispatchDefragment(
                buffer,
                newBuffer,
                moveBuffer,
                moves.Length,
                newOffset
            );

            GraphicsBuffer oldBuffer = buffer;

            buffer = newBuffer;
            capacity = newCapacity;

            for (int i = 0; i < sortedAllocations.Count; i++) {
                Allocation allocation = sortedAllocations[i];

                allocation.Buffer = buffer;
                allocation.Offset = checked(
                    (long)moves[i].DstOffset
                );
            }

            oldBuffer.Release();

            freeBlocks.Clear();

            if (newOffset < capacity) {
                freeBlocks.Add(
                    new FreeBlock(
                        newOffset,
                        capacity - newOffset
                    )
                );
            }
        }
        catch {
            newBuffer.Release();
            throw;
        }
        finally {
            moveBuffer?.Release();
        }
    }

    private void DispatchDefragment(
        GraphicsBuffer src,
        GraphicsBuffer dst,
        GraphicsBuffer moveBuffer,
        int moveCount,
        long totalLength
    ) {
        if (totalLength == 0)
            return;

        uint totalUInt4 = checked((uint)totalLength);

        uint threadCount =
            (totalUInt4 + UInt4PerThread - 1) /
            UInt4PerThread;

        uint groupCount =
            (threadCount + ThreadsPerGroup - 1) /
            ThreadsPerGroup;

        uint groupsX = Math.Min(
            groupCount,
            MaxDispatchGroups
        );

        uint groupsY =
            (groupCount + groupsX - 1) /
            groupsX;

        if (groupsY > MaxDispatchGroups) {
            throw new InvalidOperationException(
                $"The defragmentation requires {groupsX} x {groupsY} " +
                $"thread groups, exceeding the maximum supported dispatch " +
                $"size of {MaxDispatchGroups} x {MaxDispatchGroups}."
            );
        }

        defragmentShader.SetBuffer(
            defragmentKernel,
            Src,
            src
        );

        defragmentShader.SetBuffer(
            defragmentKernel,
            Dst,
            dst
        );

        defragmentShader.SetBuffer(
            defragmentKernel,
            Moves,
            moveBuffer
        );

        defragmentShader.SetInt(
            MoveCount,
            moveCount
        );

        defragmentShader.SetInt(
            TotalLength,
            checked((int)totalLength)
        );

        defragmentShader.SetInt(
            GroupsX,
            checked((int)groupsX)
        );

        defragmentShader.Dispatch(
            defragmentKernel,
            checked((int)groupsX),
            checked((int)groupsY),
            1
        );
    }

    private List<Allocation> GetSortedAllocations() {
        var result = new List<Allocation>(allocations);

        result.Sort(
            (a, b) => a.Offset.CompareTo(b.Offset)
        );

        return result;
    }

    private void RebuildFreeList() {
        RebuildFreeList(GetSortedAllocations());
    }

    private void RebuildFreeList(List<Allocation> sortedAllocations) {
        freeBlocks.Clear();

        long currentOffset = 0;

        for (int i = 0; i < sortedAllocations.Count; i++) {
            Allocation allocation = sortedAllocations[i];

            if (allocation.Offset > currentOffset) {
                freeBlocks.Add(
                    new FreeBlock(
                        currentOffset,
                        allocation.Offset - currentOffset
                    )
                );
            }

            currentOffset = checked(
                allocation.Offset + allocation.Length
            );
        }

        if (currentOffset < capacity) {
            freeBlocks.Add(
                new FreeBlock(
                    currentOffset,
                    capacity - currentOffset
                )
            );
        }
    }

    private long CalculateGrowthCapacity(long requiredCapacity) {
        if (requiredCapacity <= capacity)
            return capacity;

        long requiredCapacityBytes = requiredCapacity * UInt4Bytes;
        long currCapacity = capacity * UInt4Bytes;
        
        while (currCapacity < requiredCapacityBytes) {
            if (currCapacity < GrowthStepBytes) {
                currCapacity = GrowthStepBytes;
            } else {
                currCapacity += GrowthStepBytes;
            }
        }

        return currCapacity / UInt4Bytes;
    }

    private FreeBlock? FindBestFit(long length) {
        FreeBlock? best = null;

        for (int i = 0; i < freeBlocks.Count; i++) {
            FreeBlock block = freeBlocks[i];

            if (block.Length < length)
                continue;

            if (best == null || block.Length < best.Value.Length)
                best = block;
        }

        return best;
    }

    private void InsertFreeBlock(FreeBlock block) {
        int index = FindInsertIndex(block.Offset);

        freeBlocks.Insert(index, block);

        if (index > 0) {
            FreeBlock previous = freeBlocks[index - 1];

            if (previous.Offset + previous.Length == block.Offset) {
                block = new FreeBlock(
                    previous.Offset,
                    previous.Length + block.Length
                );

                freeBlocks[index - 1] = block;
                freeBlocks.RemoveAt(index);

                index--;
            }
        }

        if (index + 1 < freeBlocks.Count) {
            FreeBlock next = freeBlocks[index + 1];

            if (block.Offset + block.Length == next.Offset) {
                freeBlocks[index] = new FreeBlock(
                    block.Offset,
                    block.Length + next.Length
                );

                freeBlocks.RemoveAt(index + 1);
            }
        }
    }

    private int FindInsertIndex(long offset) {
        int low = 0;
        int high = freeBlocks.Count;

        while (low < high) {
            int middle = low + ((high - low) >> 1);

            if (freeBlocks[middle].Offset < offset)
                low = middle + 1;
            else
                high = middle;
        }

        return low;
    }

    private void RemoveFreeBlock(FreeBlock block) {
        int index = FindInsertIndex(block.Offset);

        if (index >= freeBlocks.Count ||
            freeBlocks[index].Offset != block.Offset) {
            throw new InvalidOperationException(
                "The requested free block could not be found."
            );
        }

        freeBlocks.RemoveAt(index);
    }

    private long GetFreeMemory() {
        long result = 0;

        for (int i = 0; i < freeBlocks.Count; i++)
            result = checked(
                result + freeBlocks[i].Length
            );

        return result;
    }

    private static GraphicsBuffer CreateBuffer(long count) {
        if (count > int.MaxValue) {
            throw new InvalidOperationException(
                $"The requested buffer contains {count} uint4 elements, " +
                "which exceeds the maximum supported GraphicsBuffer count."
            );
        }

        return new GraphicsBuffer(
            GraphicsBuffer.Target.Structured,
            checked((int)count),
            16
        );
    }

    private static long BytesToUInt4(long bytes) {
        if (bytes <= 0 || bytes % UInt4Bytes != 0) {
            throw new ArgumentException(
                "The buffer size must be a positive multiple of 16 bytes."
            );
        }

        return bytes / UInt4Bytes;
    }

    public void Dispose() {
        allocations.Clear();
        freeBlocks.Clear();

        buffer?.Release();
        buffer = null;

        capacity = 0;
    }

    private readonly struct Move {
        public readonly uint SrcOffset;
        public readonly uint DstOffset;
        public readonly uint Length;
        public readonly uint CumulativeEnd;

        public Move(
            uint srcOffset,
            uint dstOffset,
            uint length,
            uint cumulativeEnd
        ) {
            SrcOffset = srcOffset;
            DstOffset = dstOffset;
            Length = length;
            CumulativeEnd = cumulativeEnd;
        }
    }

    public sealed class Allocation {
        private readonly GpuBufferAllocator owner;

        internal GpuBufferAllocator Owner => owner;
        internal bool IsFreed { get; set; }

        public GraphicsBuffer Buffer { get; internal set; }
        public long Offset { get; internal set; }
        public long Length { get; }

        internal Allocation(
            GpuBufferAllocator owner,
            GraphicsBuffer buffer,
            long offset,
            long length
        ) {
            this.owner = owner;
            Buffer = buffer;
            Offset = offset;
            Length = length;
        }

        public void Free() {
            owner.Dealloc(this);
        }
    }

    private readonly struct FreeBlock {
        public readonly long Offset;
        public readonly long Length;

        public FreeBlock(
            long offset,
            long length
        ) {
            Offset = offset;
            Length = length;
        }
    }
}