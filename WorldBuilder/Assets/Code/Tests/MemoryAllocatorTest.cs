using System;
using Game.Utils;
using NUnit.Framework;

public class FakeDisposableBuffer : IDisposable {
    public int Capacity { get; }
    public bool IsDisposed { get; private set; }

    public FakeDisposableBuffer(int capacity) {
        Capacity = capacity;
    }

    public void Dispose() {
        IsDisposed = true;
    }
}

[TestFixture]
public class MemoryAllocatorTests {

    private MemoryAllocator<FakeDisposableBuffer> CreateAllocator(int minCap = 16, int maxCap = 256) {
        return new MemoryAllocator<FakeDisposableBuffer>(
            size => new FakeDisposableBuffer(size),
            minCap,
            maxCap
        );
    }

    #region 1. Constructor Tests

    [Test]
    public void Constructor_WithValidParameters_ShouldInstantiate() {
        Assert.DoesNotThrow(() => new MemoryAllocator<FakeDisposableBuffer>(s => new FakeDisposableBuffer(s), 16, 256));
    }

    [TestCase(0, 256, ExpectedResult = typeof(ArgumentOutOfRangeException))]
    [TestCase(-10, 256, ExpectedResult = typeof(ArgumentOutOfRangeException))]
    public Type Constructor_InvalidMinCapacity_ThrowsException(int minCap, int maxCap) {
        try {
            var _ = new MemoryAllocator<FakeDisposableBuffer>(s => new FakeDisposableBuffer(s), minCap, maxCap);
            return null;
        } catch (Exception ex) {
            return ex.GetType();
        }
    }

    [Test]
    public void Constructor_MaxCapacityLessThanMinCapacity_ThrowsArgumentOutOfRangeException() {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MemoryAllocator<FakeDisposableBuffer>(s => new FakeDisposableBuffer(s), 64, 32));
    }

    [Test]
    public void Constructor_MaxCapacityNotMultipleOfMinCapacity_ThrowsArgumentException() {
        Assert.Throws<ArgumentException>(() =>
            new MemoryAllocator<FakeDisposableBuffer>(s => new FakeDisposableBuffer(s), 10, 25));
    }

    [Test]
    public void Constructor_NullFactory_ThrowsArgumentNullException() {
        Assert.Throws<ArgumentNullException>(() =>
            new MemoryAllocator<FakeDisposableBuffer>(null, 16, 256));
    }

    #endregion

    #region 2. Allocation Edge Cases & Alignment Tests

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(-100)]
    public void Alloc_InvalidElements_ReturnsDefaultAllocation(int invalidSize) {
        var allocator = CreateAllocator(16, 256);
        var alloc = allocator.Alloc(invalidSize);

        Assert.IsNull(alloc.Node);
        Assert.AreEqual(0, alloc.Count);
        Assert.IsNull(alloc.Source);
    }

    [Test]
    public void Alloc_ElementsGreaterThanMaxCapacity_ReturnsDefaultAllocation() {
        var allocator = CreateAllocator(16, 256);
        var alloc = allocator.Alloc(257);

        Assert.IsNull(alloc.Node);
        Assert.AreEqual(0, alloc.Count);
    }

    [TestCase(1, 16)]     // < minCapacity
    [TestCase(16, 16)]    // = minCapacity
    [TestCase(17, 32)]    // > minCapacity
    [TestCase(32, 32)]    // minCapacity * 2
    [TestCase(256, 256)]  // maxCapacity
    public void Alloc_ValidSizes_AlignsCorrectly(int requestedSize, int expectedAlignedSize) {
        var allocator = CreateAllocator(16, 256);
        var alloc = allocator.Alloc(requestedSize);

        Assert.IsNotNull(alloc.Node);
        Assert.AreEqual(requestedSize, alloc.Count);
        Assert.AreEqual(0, alloc.Index);
        Assert.IsNotNull(alloc.Source);
    }

    [Test]
    public void Alloc_WhenFactoryThrowsException_ReturnsDefaultAllocationSafely() {
        var allocator = new MemoryAllocator<FakeDisposableBuffer>(
            s => throw new InvalidOperationException("Factory failed"),
            16,
            256
        );

        Allocation<FakeDisposableBuffer> alloc = default;
        Assert.DoesNotThrow(() => alloc = allocator.Alloc(16));
        Assert.IsNull(alloc.Node);
    }

    #endregion

    #region 3. Sequential Allocations & Node Splitting

    [Test]
    public void Alloc_SequentialAllocations_AdvancesNodeIndices() {
        var allocator = CreateAllocator(16, 256);

        var alloc1 = allocator.Alloc(32);
        var alloc2 = allocator.Alloc(32);
        var alloc3 = allocator.Alloc(64);

        Assert.AreEqual(0, alloc1.Index);
        Assert.AreEqual(32, alloc2.Index);
        Assert.AreEqual(64, alloc3.Index);

        Assert.AreSame(alloc1.Source, alloc2.Source);
        Assert.AreSame(alloc2.Source, alloc3.Source);
    }

    [Test]
    public void Alloc_ExceedingSingleBucketCapacity_CreatesNewBucket() {
        var allocator = CreateAllocator(16, 256);

        var alloc1 = allocator.Alloc(200); // (208) 256 - 208 = 48(32)
        var alloc2 = allocator.Alloc(200); // 200 > 48(32)

        Assert.IsNotNull(alloc1.Node);
        Assert.IsNotNull(alloc2.Node);
        Assert.AreNotSame(alloc1.Source, alloc2.Source);
        Assert.AreEqual(0, alloc2.Index);
    }

    #endregion

    #region 4. Free & Coalescing Edge Cases

    [Test]
    public void Free_SingleAllocation_AllowsReallocationAtSameIndex() {
        var allocator = CreateAllocator(16, 256);
        var alloc1 = allocator.Alloc(64);

        allocator.Free(alloc1);

        var alloc2 = allocator.Alloc(64);
        Assert.AreEqual(alloc1.Index, alloc2.Index);
    }

    [Test]
    public void Free_CoalesceRight_CombinesWithNextFreeNode() {
        var allocator = CreateAllocator(16, 256);

        var a1 = allocator.Alloc(32); // [0, 32)
        var a2 = allocator.Alloc(32); // [32, 64)
        var a3 = allocator.Alloc(32); // [64, 96)

        allocator.Free(a2);
        allocator.Free(a1);

        var aBig = allocator.Alloc(64); // [0, 64)
        Assert.AreEqual(0, aBig.Index);
    }

    [Test]
    public void Free_CoalesceLeft_CombinesWithPrevFreeNode() {
        var allocator = CreateAllocator(16, 256);

        var a1 = allocator.Alloc(32); // [0, 32)
        var a2 = allocator.Alloc(32); // [32, 64)

        allocator.Free(a1);
        allocator.Free(a2);

        var aBig = allocator.Alloc(64);
        Assert.AreEqual(0, aBig.Index);
    }

    [Test]
    public void Free_CoalesceBothLeftAndRight_CombinesThreeNodesIntoOne() {
        var allocator = CreateAllocator(16, 256);

        var a1 = allocator.Alloc(32); // [0, 32)
        var a2 = allocator.Alloc(32); // [32, 64)
        var a3 = allocator.Alloc(32); // [64, 96)

        allocator.Free(a1);
        allocator.Free(a3);

        allocator.Free(a2);

        var aBig = allocator.Alloc(96);
        Assert.AreEqual(0, aBig.Index);
    }

    [Test]
    public void Free_InvalidOrDefaultAllocation_DoesNotThrow() {
        var allocator = CreateAllocator(16, 256);
        Allocation<FakeDisposableBuffer> invalidAlloc = default;

        Assert.DoesNotThrow(() => allocator.Free(invalidAlloc));
    }

    [Test]
    public void Free_DoubleFree_DoesNotThrowOrCorruptState() {
        var allocator = CreateAllocator(16, 256);
        var alloc = allocator.Alloc(32);

        allocator.Free(alloc);
        Assert.DoesNotThrow(() => allocator.Free(alloc));
    }

    [Test]
    public void Free_AllocationFromAnotherAllocator_IsIgnoredSafely() {
        var allocator1 = CreateAllocator(16, 256);
        var allocator2 = CreateAllocator(16, 256);

        var alloc1 = allocator1.Alloc(32);

        Assert.DoesNotThrow(() => allocator2.Free(alloc1));
    }

    #endregion

    #region 5. Bucket & Memory Lifecycle (Dispose / Release)

    [Test]
    public void ReleaseUnusedBuckets_FreesCompletelyEmptyBuckets() {
        var allocator = CreateAllocator(16, 256);

        var a1 = allocator.Alloc(256);
        var a2 = allocator.Alloc(256);

        FakeDisposableBuffer buffer1 = a1.Source;
        FakeDisposableBuffer buffer2 = a2.Source;

        allocator.Free(a1);

        Assert.IsFalse(buffer1.IsDisposed);

        allocator.ReleaseUnusedBuckets();

        Assert.IsTrue(buffer1.IsDisposed);
        Assert.IsFalse(buffer2.IsDisposed);
    }

    [Test]
    public void ReleaseUnusedBuckets_DoesNotReleasePartiallyUsedBuckets() {
        var allocator = CreateAllocator(16, 256);

        var a1 = allocator.Alloc(128);
        var a2 = allocator.Alloc(64);

        allocator.Free(a1);

        allocator.ReleaseUnusedBuckets();

        Assert.IsFalse(a1.Source.IsDisposed);
    }

    [Test]
    public void Dispose_DisposesAllAllocatedBucketBuffers() {
        var allocator = CreateAllocator(16, 256);

        var a1 = allocator.Alloc(200);
        var a2 = allocator.Alloc(200);

        FakeDisposableBuffer buffer1 = a1.Source;
        FakeDisposableBuffer buffer2 = a2.Source;

        allocator.Dispose();

        Assert.IsTrue(buffer1.IsDisposed);
        Assert.IsTrue(buffer2.IsDisposed);
    }

    #endregion

    #region 6. Complex Stress & Fragmentation Scenarios

    [Test]
    public void Stress_InterleavedAllocAndFree_MaintainsIntegrity() {
        var allocator = CreateAllocator(16, 512);
        var allocs = new Allocation<FakeDisposableBuffer>[10];

        for (int i = 0; i < allocs.Length; i++) {
            allocs[i] = allocator.Alloc(32);
            Assert.IsNotNull(allocs[i].Node);
        }

        for (int i = 0; i < allocs.Length; i += 2) {
            allocator.Free(allocs[i]);
        }

        for (int i = 0; i < allocs.Length; i += 2) {
            var realloc = allocator.Alloc(32);
            Assert.IsNotNull(realloc.Node);
        }

        for (int i = 1; i < allocs.Length; i += 2) {
            allocator.Free(allocs[i]);
        }

        allocator.ReleaseUnusedBuckets();
    }

    #endregion
}