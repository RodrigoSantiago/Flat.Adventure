using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Code.Data;
using Game.Worlds.Storage;
using NUnit.Framework;

[TestFixture]
public class RegionSerializeTests {
    private InMemoryStreamTransfer _transfer;
    private RegionSerialize _serializer;

    [SetUp]
    public void SetUp() {
        _transfer = new InMemoryStreamTransfer();
        _serializer = new RegionSerialize(_transfer);
    }

    // ============================================================
    // Basic save/load
    // ============================================================

    [Test]
    public void Save_NewFile_ThenLoad_ReturnsSameData() {
        var updates = new[] {
            CreateChunk(0, 1, 100, 200, 300, 400),
            CreateChunk(1, 2, 10, 20, 30, 40),
            CreateChunk(2, 3, 1, 2, 3, 4)
        };

        _serializer.Save("region.dat", updates, update: false);

        var loaded = _serializer.Load("region.dat", -1);

        Assert.That(loaded.Length, Is.EqualTo(updates.Length));

        foreach (var expected in updates) {
            var actual = loaded.Single(x => x.chunkEntryId == expected.chunkEntryId);

            AssertChunkEqual(expected, actual);
        }
    }

    [Test]
    public void Save_NewFile_WithEmptyChunks_LoadsEmptyChunks() {
        var updates = new[] {
            CreateChunk(0, 10),
            CreateChunk(1, 20),
            CreateChunk(2, 30)
        };

        _serializer.Save("region.dat", updates, false);

        var loaded = _serializer.Load("region.dat", -1);

        Assert.That(loaded.Length, Is.EqualTo(3));

        foreach (var chunk in loaded) {
            Assert.That(chunk.soilDenData, Is.Null);
            Assert.That(chunk.soilMatData, Is.Null);
            Assert.That(chunk.meshData, Is.Null);
            Assert.That(chunk.listData, Is.Null);
        }
    }

    [Test]
    public void Save_NewFile_WithOnlyOneDataType_Works() {
        var chunk = new ChunkCacheUpdate {
            chunkEntryId = 0,
            version = 123,
            soilDenData = Bytes(100, 1)
        };

        _serializer.Save("region.dat", new[] { chunk }, false);

        var loaded = _serializer.Load("region.dat", -1);

        var actual = loaded.Single(x => x.chunkEntryId == 0);

        Assert.That(actual.version, Is.EqualTo(123));
        CollectionAssert.AreEqual(chunk.soilDenData, actual.soilDenData);
        Assert.That(actual.soilMatData, Is.Null);
        Assert.That(actual.meshData, Is.Null);
        Assert.That(actual.listData, Is.Null);
    }

    [Test]
    public void Save_NewFile_WithAllDataTypes_Works() {
        var chunk = CreateChunk(5, 987, 1024, 2048, 4096, 8192);

        _serializer.Save("region.dat", new[] { chunk }, false);

        var loaded = _serializer.Load("region.dat", -1);

        AssertChunkEqual(chunk, loaded.Single(x => x.chunkEntryId == 5));
    }

    // ============================================================
    // Repeated saves
    // ============================================================

    [Test]
    public void ManyConsecutiveSaves_PreserveLatestVersion() {
        const int saveCount = 100;

        for (int i = 0; i < saveCount; i++) {
            var chunk = CreateChunk(0, i + 1, 50 + i, 100 + i, 150 + i, 200 + i);

            _serializer.Save("region.dat", new[] { chunk }, update: i > 0);
        }

        var loaded = _serializer.Load("region.dat", -1);

        var actual = loaded.Single(x => x.chunkEntryId == 0);

        Assert.That(actual.version, Is.EqualTo(saveCount));

        Assert.That(actual.soilDenData.Length, Is.EqualTo(50 + saveCount - 1));
        Assert.That(actual.soilMatData.Length, Is.EqualTo(100 + saveCount - 1));
        Assert.That(actual.meshData.Length, Is.EqualTo(150 + saveCount - 1));
        Assert.That(actual.listData.Length, Is.EqualTo(200 + saveCount - 1));
    }

    [Test]
    public void ManyChunks_ManySaves_PreserveAllLatestData() {
        var expected = new Dictionary<int, ChunkCacheUpdate>();

        // Initial save
        for (int i = 0; i < Math.Min(Region.TotalChunks, 32); i++) {
            var chunk = CreateRandomChunk(i, 1, 100 + i);

            expected[i] = chunk;

            _serializer.Save("region.dat", new[] { chunk }, update: i != 0);
        }

        // Update every chunk several times
        for (int round = 2; round <= 20; round++) {
            for (int i = 0; i < Math.Min(Region.TotalChunks, 32); i++) {
                var chunk = CreateRandomChunk(i, round, 20 + ((round * 13 + i * 7) % 200));

                expected[i] = chunk;

                _serializer.Save("region.dat", new[] { chunk }, update: true);
            }
        }

        var loaded = _serializer.Load("region.dat", -1);

        foreach (var pair in expected) {
            var actual = loaded.Single(x => x.chunkEntryId == pair.Key);

            AssertChunkEqual(pair.Value, actual);
        }
    }

    [Test]
    public void UpdatingChunk_WithLargerData_PreservesOtherChunks() {
        var first = CreateChunk(0, 1, 100, 100, 100, 100);
        var second = CreateChunk(1, 1, 100, 100, 100, 100);

        _serializer.Save("region.dat", new[] { first, second }, false);

        var replacement = CreateChunk(0, 2, 5000, 6000, 7000, 8000);

        _serializer.Save("region.dat", new[] { replacement }, true);

        var loaded = _serializer.Load("region.dat", -1);

        AssertChunkEqual(replacement, loaded.Single(x => x.chunkEntryId == 0));

        AssertChunkEqual(second, loaded.Single(x => x.chunkEntryId == 1));
    }

    [Test]
    public void UpdatingChunk_WithSmallerData_PreservesOtherChunks() {
        var first = CreateChunk(0, 1, 5000, 5000, 5000, 5000);
        var second = CreateChunk(1, 1, 5000, 5000, 5000, 5000);

        _serializer.Save("region.dat", new[] { first, second }, false);

        var replacement = CreateChunk(0, 2, 1, 2, 3, 4);

        _serializer.Save("region.dat", new[] { replacement }, true);

        var loaded = _serializer.Load("region.dat", -1);

        AssertChunkEqual(replacement, loaded.Single(x => x.chunkEntryId == 0));

        AssertChunkEqual(second, loaded.Single(x => x.chunkEntryId == 1));
    }

    // ============================================================
    // Delete / empty update
    // ============================================================

    [Test]
    public void UpdatingChunkWithZeroLength_RemovesChunkData() {
        var original = CreateChunk(0, 1, 100, 100, 100, 100);

        _serializer.Save("region.dat", new[] { original }, false);

        var deleted = new ChunkCacheUpdate {
            chunkEntryId = 0,
            version = 2
        };

        _serializer.Save("region.dat", new[] { deleted }, true);

        var loaded = _serializer.Load("region.dat", -1);

        var actual = loaded.Single(x => x.chunkEntryId == 0);

        Assert.That(actual.version, Is.EqualTo(2));
        Assert.That(actual.soilDenData, Is.Null);
        Assert.That(actual.soilMatData, Is.Null);
        Assert.That(actual.meshData, Is.Null);
        Assert.That(actual.listData, Is.Null);
    }

    // ============================================================
    // LOD
    // ============================================================

    [Test]
    public void Load_WithRequiredLods_DoesNotReturnUnrequiredChunks() {
        var updates = new List<ChunkCacheUpdate>();

        for (int i = 0; i < Region.TotalChunks; i++) {
            updates.Add(CreateChunk(i, i + 1, 10, 20, 30, 40));
        }

        _serializer.Save("region.dat", updates.ToArray(), false);

        var all = _serializer.Load("region.dat", -1);

        Assert.That(all.Length, Is.EqualTo(Region.TotalChunks));

        var requiredIds = Enumerable
            .Range(0, Region.TotalChunks)
            .Where(i => Region.IsRequired(Region.GetLod(i), 0))
            .ToHashSet();

        var lodZero = _serializer.Load("region.dat", 0);

        Assert.That(lodZero.All(x => requiredIds.Contains(x.chunkEntryId)), Is.True);
    }

    // ============================================================
    // Large data
    // ============================================================

    [Test]
    public void Save_LargeChunk_Works() {
        var chunk = CreateChunk(
            0,
            42,
            1_000_000,
            1_000_000,
            2_000_000,
            500_000);

        _serializer.Save("region.dat", new[] { chunk }, false);

        var loaded = _serializer.Load("region.dat", -1);

        AssertChunkEqual(chunk, loaded.Single(x => x.chunkEntryId == 0));
    }

    [Test]
    public void Save_ManyLargeChunks_Works() {
        var chunks = new List<ChunkCacheUpdate>();

        for (int i = 0; i < Math.Min(Region.TotalChunks, 16); i++) {
            chunks.Add(CreateChunk(
                i,
                i + 100,
                100_000 + i * 10,
                120_000 + i * 10,
                150_000 + i * 10,
                80_000 + i * 10));
        }

        _serializer.Save("region.dat", chunks.ToArray(), false);

        var loaded = _serializer.Load("region.dat", -1);

        foreach (var expected in chunks) {
            AssertChunkEqual(expected, loaded.Single(x => x.chunkEntryId == expected.chunkEntryId));
        }
    }

    // ============================================================
    // Randomized stress test
    // ============================================================

    [Test]
    public void RandomizedStress_ManySaves_PreserveLatestState() {
        var random = new Random(123456);

        var expected = new Dictionary<int, ChunkCacheUpdate>();

        for (int operation = 0; operation < 500; operation++) {
            int id = random.Next(0, Math.Min(Region.TotalChunks, 32));

            bool remove = random.NextDouble() < 0.10;

            ChunkCacheUpdate update;

            if (remove) {
                update = new ChunkCacheUpdate {
                    chunkEntryId = id,
                    version = operation + 1
                };
            } else {
                update = CreateRandomChunk(id, operation + 1, random.Next(0, 20_000), random);
            }

            _serializer.Save("region.dat", new[] { update }, update: _transfer.Exists("region.dat"));

            expected[id] = update;
        }

        var loaded = _serializer.Load("region.dat", -1);

        foreach (var pair in expected) {
            var actual = loaded.Single(x => x.chunkEntryId == pair.Key);

            AssertChunkEqual(pair.Value, actual);
        }
    }

    // ============================================================
    // Header corruption
    // ============================================================

    [Test]
    public void CorruptedHeaderA_WithValidHeaderB_LoadsSuccessfully() {
        var chunk = CreateChunk(0, 1, 100, 200, 300, 400);

        _serializer.Save("region.dat", new[] { chunk }, false);

        int headerA = GetHeaderA();

        _transfer.MutateFile(
            "region.dat",
            bytes => {
                for (int i = headerA; i < headerA + 16; i++)
                    bytes[i] ^= 0xFF;
            });

        var loaded = _serializer.Load("region.dat", -1);

        AssertChunkEqual(chunk, loaded.Single(x => x.chunkEntryId == 0));
    }

    [Test]
    public void CorruptedHeaderB_WithValidHeaderA_LoadsSuccessfully() {
        var chunk = CreateChunk(0, 1, 100, 200, 300, 400);

        _serializer.Save("region.dat", new[] { chunk }, false);

        int headerB = GetHeaderB();

        _transfer.MutateFile(
            "region.dat",
            bytes => {
                for (int i = headerB; i < headerB + 16; i++)
                    bytes[i] ^= 0xFF;
            });

        var loaded = _serializer.Load("region.dat", -1);

        AssertChunkEqual(chunk, loaded.Single(x => x.chunkEntryId == 0));
    }

    [Test]
    public void BothHeadersCorrupted_LoadThrowsInvalidData() {
        var chunk = CreateChunk(0, 1, 100, 200, 300, 400);

        _serializer.Save("region.dat", new[] { chunk }, false);

        int headerSize = GetHeaderSize();
        
        _transfer.MutateFile(
            "region.dat",
            bytes => {
                for (int i = 0; i < headerSize; i++)
                    bytes[i] = 0xFF;
            });

        Assert.Throws<InvalidDataException>(() => _serializer.Load("region.dat", -1));
    }

    [Test]
    public void TruncatedHeader_LoadThrowsInvalidData() {
        var chunk = CreateChunk(0, 1, 100, 200, 300, 400);

        _serializer.Save("region.dat", new[] { chunk }, false);

        _transfer.TruncateFile("region.dat", GetHeaderSize() - 1);

        Assert.Throws<InvalidDataException>(() => _serializer.Load("region.dat", -1));
    }

    [Test]
    public void EmptyFile_LoadThrowsInvalidData() {
        _transfer.CreateRawFile("region.dat", Array.Empty<byte>());

        Assert.Throws<InvalidDataException>(() => _serializer.Load("region.dat", -1));
    }

    [Test]
    public void RandomGarbageFile_LoadThrowsInvalidData() {
        _transfer.CreateRawFile("region.dat", RandomBytes(GetHeaderSize() + 1000, new Random(123)));

        Assert.Throws<InvalidDataException>(() => _serializer.Load("region.dat", -1));
    }

    // ============================================================
    // Invalid offsets / lengths
    // ============================================================

    [Test]
    public void InvalidNegativeOffset_HeaderIsRejected() {
        var chunk = CreateChunk(0, 1, 100, 100, 100, 100);

        _serializer.Save("region.dat", new[] { chunk }, false);

        int entryOffsetA = GetHeaderA();
        int entryOffsetB = GetHeaderB();

        _transfer.MutateFile(
            "region.dat",
            bytes => {
                WriteLong(bytes, entryOffsetA, -1);
                WriteLong(bytes, entryOffsetB, -1);
            });

        Assert.Throws<InvalidDataException>(() => _serializer.Load("region.dat", -1));
    }

    [Test]
    public void InvalidNegativeLength_HeaderIsRejected() {
        var chunk = CreateChunk(0, 1, 100, 100, 100, 100);

        _serializer.Save("region.dat", new[] { chunk }, false);

        // offset + sizeof(long)
        int lengthOffsetA = GetHeaderA() + sizeof(long);
        int lengthOffsetB = GetHeaderB() + sizeof(long);

        _transfer.MutateFile(
            "region.dat",
            bytes => {
                WriteLong(bytes, lengthOffsetA, -1); 
                WriteLong(bytes, lengthOffsetB, -1);
            });

        Assert.Throws<InvalidDataException>(() =>
            _serializer.Load("region.dat", -1));
    }

    [Test]
    public void DataLengthsGreaterThanEntryLength_HeaderIsRejected() {
        var chunk = CreateChunk(0, 1, 100, 100, 100, 100);

        _serializer.Save("region.dat", new[] { chunk }, false);

        // Entry layout:
        //
        // long offset
        // long length
        // int version
        // int soilDenLength

        int soilDenLengthOffsetA = GetHeaderA() + sizeof(long) + sizeof(long) + sizeof(int);
        int soilDenLengthOffsetB = GetHeaderB() + sizeof(long) + sizeof(long) + sizeof(int);

        _transfer.MutateFile(
            "region.dat",
            bytes => {
                WriteInt(bytes, soilDenLengthOffsetA, int.MaxValue);
                WriteInt(bytes, soilDenLengthOffsetB, int.MaxValue);
            });

        Assert.Throws<InvalidDataException>(() => _serializer.Load("region.dat", -1));
    }

    [Test]
    public void OffsetBeforeHeader_HeaderIsRejected() {
        var chunk = CreateChunk(0, 1, 100, 100, 100, 100);

        _serializer.Save("region.dat", new[] { chunk }, false);

        int offsetA = GetHeaderA();
        int offsetB = GetHeaderB();
        
        _transfer.MutateFile(
            "region.dat",
            bytes => {
                WriteLong(bytes, offsetA, 1);
                WriteLong(bytes, offsetB, 1);
            });

        Assert.Throws<InvalidDataException>(() => _serializer.Load("region.dat", -1));
    }

    // ============================================================
    // Simulated failures
    // ============================================================

    [Test]
    public void FailureDuringInitialDataWrite_DoesNotCreateValidFile() {
        _transfer.FailOnWriteNumber = 2;

        var chunk = CreateChunk(0, 1, 1000, 1000, 1000, 1000);

        Assert.Throws<IOException>(() => _serializer.Save("region.dat", new[] { chunk }, false));

        Assert.That(_transfer.Exists("region.dat"), Is.True);

        Assert.Throws<InvalidDataException>(() => _serializer.Load("region.dat", -1));
    }

    [Test]
    public void FailureDuringHeaderWrite_CanLeaveRecoverableHeader() {
        var original = CreateChunk(0, 1, 100, 100, 100, 100);

        _serializer.Save("region.dat", new[] { original }, false);

        var replacement = CreateChunk(0, 2, 200, 200, 200, 200);

        /*
         * Header writing:
         *   Write Magic
         *   Write A
         *   Flush
         *   Write B
         *   Flush
         */
        _transfer.FailOnWriteNumber = _transfer.TotalWrites + (_transfer.TotalWrites - 2);

        Assert.Throws<IOException>(() => _serializer.Save("region.dat", new[] { replacement }, true));

        var loaded = _serializer.Load("region.dat", -1);
        
        var actual = loaded.Single(x => x.chunkEntryId == 0);
        
        Assert.That(actual.version == 1, Is.True); // Keep B
    }

    [Test]
    public void FailureDuringSecondHeaderWrite_OldOrNewHeaderMustRemainValid() {
        var original = CreateChunk(0, 1, 100, 100, 100, 100);

        _serializer.Save("region.dat", new[] { original }, false);

        var replacement = CreateChunk(0, 2, 500, 600, 700, 800);

        /*
         * Header writing:
         *   Write Magic
         *   Write A
         *   Flush
         *   Write B
         *   Flush
         */
        _transfer.FailOnWriteNumber = _transfer.TotalWrites + (_transfer.TotalWrites - 1);

        Assert.Throws<IOException>(() => _serializer.Save("region.dat", new[] { replacement }, true));

        var loaded = _serializer.Load("region.dat", -1);

        var actual = loaded.Single(x => x.chunkEntryId == 0);

        Assert.That(actual.version == 1, Is.True); // Keep A
    }

    [Test]
    public void FailureDuringFlush_DoesNotProduceInvalidCommittedState() {
        var original = CreateChunk(0, 1, 100, 100, 100, 100);

        _serializer.Save("region.dat", new[] { original }, false);

        var replacement = CreateChunk(0, 2, 1000, 1000, 1000, 1000);

        _transfer.FailOnFlushNumber = _transfer.TotalFlushes + 1;

        Assert.Throws<IOException>(() => _serializer.Save("region.dat", new[] { replacement }, true));

        Assert.DoesNotThrow(() => {
            try {
                _serializer.Load("region.dat", -1);
            } catch (InvalidDataException) {
                
            }
        });
    }

    // ============================================================
    // Compaction
    // ============================================================

    [Test]
    public void RepeatedUpdates_EventuallyTriggerCompaction_AndPreserveData() {
        var original = new[] {
            CreateChunk(0, 1, 1000, 1000, 1000, 1000),
            CreateChunk(1, 1, 1, 1, 1, 1),
            CreateChunk(2, 1, 1000, 1000, 1000, 1000),
            CreateChunk(3, 1, 1, 1, 1, 1),
            CreateChunk(4, 1, 1000, 1000, 1000, 1000),
            CreateChunk(5, 1, 1, 1, 1, 1),
            CreateChunk(6, 1, 1000, 1000, 1000, 1000),
            CreateChunk(7, 1, 1, 1, 1, 1),
        };

        var upgrade1 = new[] {
            CreateChunk(0, 1, 1, 1, 1, 1),
            CreateChunk(2, 1, 1, 1, 1, 1),
            CreateChunk(4, 1, 1, 1, 1, 1),
            CreateChunk(6, 1, 1, 1, 1, 1),
        };

        var expected = new[] {
            upgrade1[0],
            original[1],
            upgrade1[1],
            original[3],
            upgrade1[2],
            original[5],
            upgrade1[3],
            original[7],
        };
        
        long size = GetHeaderSize();
        for (int i = 0; i < expected.Length; i++) {
            size += expected[i].TotalLength;
        }
        
        _serializer.Save("region.dat", original, false);

        _serializer.Save("region.dat", upgrade1, true);

        var loaded = _serializer.Load("region.dat", -1);
        var after = _transfer.ReadRawFile("region.dat");

        foreach (var expectedChunk in expected) {
            var actual = loaded.Single(x => x.chunkEntryId == expectedChunk.chunkEntryId);

            AssertChunkEqual(expectedChunk, actual);
        }
        Assert.That(after.Length, Is.LessThan((int)(size * 2.5f)));
    }

    [Test]
    public void CompactionFailure_DoesNotReplaceOriginalFile() {
        var original = new[] {
            CreateChunk(0, 1, 1000, 1000, 1000, 1000),
            CreateChunk(1, 1, 1, 1, 1, 1),
            CreateChunk(2, 1, 1000, 1000, 1000, 1000),
            CreateChunk(3, 1, 1, 1, 1, 1),
            CreateChunk(4, 1, 1000, 1000, 1000, 1000),
            CreateChunk(5, 1, 1, 1, 1, 1),
            CreateChunk(6, 1, 1000, 1000, 1000, 1000),
            CreateChunk(7, 1, 1, 1, 1, 1),
        };

        _serializer.Save("region.dat", original, false);
        
        var before = _transfer.ReadRawFile("region.dat");

        _transfer.FailOnlyTempWrites = true;
        _transfer.FailOnWriteNumber = _transfer.TotalWrites + 2;

        Assert.Throws<IOException>(() => {

            var upgrade1 = new[] {
                CreateChunk(0, 1, 1, 1, 1, 1),
                CreateChunk(2, 1, 1, 1, 1, 1),
                CreateChunk(4, 1, 1, 1, 1, 1),
                CreateChunk(6, 1, 1, 1, 1, 1),
            };

            _serializer.Save("region.dat", upgrade1, true);
        });
        
        Assert.That(_transfer.Exists("region.dat"), Is.True);

        var after = _transfer.ReadRawFile("region.dat");

        Assert.That(after, Is.EqualTo(before).Or.Not.Null);
    }

    [Test]
    public void TempUpgradeFailure_DoesNotCorruptOriginal() {
        var original = new[] {
            CreateChunk(0, 1, 1000, 1000, 1000, 1000),
            CreateChunk(1, 1, 1, 1, 1, 1),
            CreateChunk(2, 1, 1000, 1000, 1000, 1000),
            CreateChunk(3, 1, 1, 1, 1, 1),
            CreateChunk(4, 1, 1000, 1000, 1000, 1000),
            CreateChunk(5, 1, 1, 1, 1, 1),
            CreateChunk(6, 1, 1000, 1000, 1000, 1000),
            CreateChunk(7, 1, 1, 1, 1, 1),
        };

        _serializer.Save("region.dat", original, false);

        Assert.Throws<IOException>(() => {
            _transfer.FailUpgradeTempOutput = true;
        
            var upgrade1 = new[] {
                CreateChunk(0, 1, 1, 1, 1, 1),
                CreateChunk(2, 1, 1, 1, 1, 1),
                CreateChunk(4, 1, 1, 1, 1, 1),
                CreateChunk(6, 1, 1, 1, 1, 1),
            };

            _serializer.Save("region.dat", upgrade1, true);
        });

        Assert.That(_transfer.Exists("region.dat"), Is.True);

        Assert.DoesNotThrow(() => {
            _serializer.Load("region.dat", -1);
        });
    }

    // ============================================================
    // Concurrent-ish / stream behavior
    // ============================================================

    [Test]
    public void LoadingAfterEverySave_RemainsConsistent() {
        var random = new Random(555);

        var expected = new Dictionary<int, ChunkCacheUpdate>();

        for (int i = 0; i < 200; i++) {
            int id = random.Next(0, Math.Min(Region.TotalChunks, 16));

            var chunk = CreateRandomChunk(id, i + 1, random.Next(1, 5000), random);

            expected[id] = chunk;

            _serializer.Save("region.dat", new[] { chunk }, update: _transfer.Exists("region.dat"));

            var loaded = _serializer.Load("region.dat", -1);

            foreach (var pair in expected) {
                var actual = loaded.SingleOrDefault(x => x.chunkEntryId == pair.Key);

                Assert.That(actual, Is.Not.Null, $"Chunk {pair.Key} disappeared at operation {i}");
                AssertChunkEqual(pair.Value, actual);
            }
        }
    }

    // ============================================================
    // Helpers
    // ============================================================

    private static ChunkCacheUpdate CreateChunk(
        int id,
        int version,
        int soilDenLength = 0,
        int soilMatLength = 0,
        int meshLength = 0,
        int listLength = 0) {
        return new ChunkCacheUpdate {
            chunkEntryId = id,
            version = version,
            soilDenData = soilDenLength > 0 ? GeneratePattern(soilDenLength, (byte)(id + 1)) : null,
            soilMatData = soilMatLength > 0 ? GeneratePattern(soilMatLength, (byte)(id + 11)) : null,
            meshData = meshLength > 0 ? GeneratePattern(meshLength, (byte)(id + 21)) : null,
            listData = listLength > 0 ? GeneratePattern(listLength, (byte)(id + 31)) : null
        };
    }

    private static ChunkCacheUpdate CreateRandomChunk(int id, int version, int maxSize, Random random = null) {
        random ??= new Random(id * 1000003 + version);

        int soilDen = random.Next(0, maxSize + 1);
        int soilMat = random.Next(0, maxSize + 1);
        int mesh = random.Next(0, maxSize + 1);
        int list = random.Next(0, maxSize + 1);

        return new ChunkCacheUpdate {
            chunkEntryId = id,
            version = version,
            soilDenData = soilDen == 0 ? null : RandomBytes(soilDen, random),
            soilMatData = soilMat == 0 ? null : RandomBytes(soilMat, random),
            meshData = mesh == 0 ? null : RandomBytes(mesh, random),
            listData = list == 0 ? null : RandomBytes(list, random)
        };
    }

    private static byte[] Bytes(int length, byte value) {
        var result = new byte[length];

        for (int i = 0; i < result.Length; i++)
            result[i] = value;

        return result;
    }

    private static byte[] GeneratePattern(int length, byte seed) {
        var result = new byte[length];

        for (int i = 0; i < result.Length; i++) {
            unchecked {
                result[i] = (byte)(seed + i * 31 + (i >> 8));
            }
        }

        return result;
    }

    private static byte[] RandomBytes(int length, Random random) {
        var result = new byte[length];

        random.NextBytes(result);

        return result;
    }

    private static void AssertChunkEqual(ChunkCacheUpdate expected, ChunkCacheUpdate actual) {
        Assert.That(actual.chunkEntryId, Is.EqualTo(expected.chunkEntryId));
        Assert.That(actual.version, Is.EqualTo(expected.version));
        CollectionAssert.AreEqual(expected.soilDenData, actual.soilDenData);
        CollectionAssert.AreEqual(expected.soilMatData, actual.soilMatData);
        CollectionAssert.AreEqual(expected.meshData, actual.meshData);
        CollectionAssert.AreEqual(expected.listData, actual.listData);
    }

    private static int GetHeaderSize() {
        /*
         * EntryOffset =
         *   sizeof(long) * 2 +
         *   sizeof(int) * 5
         *
         * HeaderOffset =
         *   (sizeof(int) + EntryOffset * TotalChunks) * 2
         */
        int entrySize =
            sizeof(long) * 2 +
            sizeof(int) * 5;

        return
            (sizeof(int) +
             entrySize * Region.TotalChunks) * 2 + 4;
    }

    private static int GetHeaderA() {
        return 4;
    }

    private static int GetHeaderB() {
        return (GetHeaderSize() - 4) / 2 + 4;
    }

    private static void WriteInt(
        byte[] buffer,
        int offset,
        int value) {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }

    private static void WriteLong(
        byte[] buffer,
        int offset,
        long value) {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
        buffer[offset + 4] = (byte)(value >> 32);
        buffer[offset + 5] = (byte)(value >> 40);
        buffer[offset + 6] = (byte)(value >> 48);
        buffer[offset + 7] = (byte)(value >> 56);
    }

    // ============================================================
    // In-memory IStreamTransfer
    // ============================================================

    private sealed class InMemoryStreamTransfer : IStreamTransfer {
        private readonly Dictionary<string, byte[]> _files = new();

        public int TotalWrites { get; private set; }

        public int TotalFlushes { get; private set; }

        public int? FailOnWriteNumber { get; set; }

        public int? FailOnFlushNumber { get; set; }

        public bool FailOnlyTempWrites { get; set; }

        public bool FailUpgradeTempOutput { get; set; }

        public Stream CreateInput(string file) {
            if (!_files.TryGetValue(file, out var data))
                throw new FileNotFoundException(file);

            return new ByteArrayStream(data, false, this, file, false);
        }

        public Stream CreateOutput(string file, bool temp) {
            string actualName = file + (temp ? ".tmp" : "");

            if (!_files.TryGetValue(actualName, out var data)) {
                data = Array.Empty<byte>();
            }

            return new ByteArrayStream(data, true, this, actualName, temp);
        }

        public void Flush(Stream stream) {
            TotalFlushes++;

            if (FailOnFlushNumber.HasValue && TotalFlushes >= FailOnFlushNumber.Value) {
                throw new IOException("Simulated flush failure.");
            }

            stream.Flush();
        }

        public void UpgradeTempOutput(string file) {
            if (FailUpgradeTempOutput) { 
                throw new IOException("Simulated atomic replace failure.");
            }

            string temp = file + ".tmp";

            if (!_files.ContainsKey(temp))
                throw new FileNotFoundException(temp);

            string backup = file + ".tmp.bkp";

            if (_files.TryGetValue(file, out var original)) {
                _files[backup] = (byte[])original.Clone();
            }

            _files[file] = _files[temp];
            _files.Remove(temp);
        }

        public bool Exists(string file) {
            return _files.ContainsKey(file);
        }

        public byte[] ReadRawFile(string file) {
            if (!_files.TryGetValue(file, out var bytes)) {
                throw new FileNotFoundException(file);
            }

            return (byte[])bytes.Clone();
        }

        public void CreateRawFile(string file, byte[] data) {
            _files[file] = (byte[])data.Clone();
        }

        public void TruncateFile(string file, int length) {
            if (!_files.TryGetValue(file, out var data)) {
                throw new FileNotFoundException(file);
            }

            Array.Resize(ref data, length);
            _files[file] = data;
        }

        public void MutateFile(string file, Action<byte[]> mutation) {
            if (!_files.TryGetValue(file, out var data)) {
                throw new FileNotFoundException(file);
            }

            mutation(data);
        }

        internal void OnWrite(string fileName, bool temporary) {
            TotalWrites++;

            bool shouldFail = !FailOnlyTempWrites || temporary;

            if (shouldFail &&
                FailOnWriteNumber.HasValue &&
                TotalWrites >=
                FailOnWriteNumber.Value) {
                throw new IOException("Simulated write failure.");
            }
        }

        internal void Commit(string fileName, byte[] data) {
            _files[fileName] = (byte[])data.Clone();
        }
    }

    private sealed class ByteArrayStream : Stream {
        private readonly InMemoryStreamTransfer owner;
        private readonly string fileName;
        private readonly bool temporary;
        private readonly bool writable;
        
        private byte[] buffer;
        private int length;
        
        private long position;
        private bool disposed;
        
        private const int InitialCapacity = 256;

        public ByteArrayStream(byte[] initialData, bool writable, InMemoryStreamTransfer owner, string fileName, bool temporary) {
            
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.fileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            this.temporary = temporary;
            this.writable = writable;
            
            if (initialData == null || initialData.Length == 0) {
                buffer = new byte[InitialCapacity];
                length = 0;
            } else {
                buffer = new byte[Math.Max(InitialCapacity, initialData.Length)];
                Buffer.BlockCopy(initialData, 0, buffer, 0, initialData.Length);
                length = initialData.Length;
            }

            position = 0;
        }

        public override bool CanRead => !disposed;

        public override bool CanSeek => !disposed;

        public override bool CanWrite => !disposed && writable;

        public override long Length {
            get {
                ThrowIfDisposed();
                return length;
            }
        }

        public override long Position {
            get {
                ThrowIfDisposed();
                return position;
            }
            set {
                ThrowIfDisposed();
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                position = value;
            }
        }

        public override void Flush() {
            ThrowIfDisposed();
        }

        public override int Read(byte[] buffer, int offset, int count) {
            ThrowIfDisposed();
            ValidateBufferArguments(buffer, offset, count);
            
            if (count == 0) return 0;
            if (position >= length) return 0;
            
            long remaining = length - position;
            int toRead = (int)Math.Min(remaining, count);
            Buffer.BlockCopy(this.buffer, checked((int)position), buffer, offset, toRead);
            position += toRead;
            
            return toRead;
        }

        public override int ReadByte() {
            ThrowIfDisposed();
            
            if (position >= length) return -1;
            
            byte value = buffer[checked((int)position)];
            position++;
            
            return value;
        }

        public override void Write(byte[] buffer, int offset, int count) {
            ThrowIfDisposed();
            
            if (!writable) throw new NotSupportedException("Stream is not writable.");
            
            ValidateBufferArguments(buffer, offset, count);
            
            if (count == 0) return;
            
            owner.OnWrite(fileName, temporary);
            
            long endPosition = checked(position + count);
            EnsureCapacity(endPosition);
            Buffer.BlockCopy(buffer, offset, this.buffer, checked((int)position), count);
            position = endPosition;
            if (position > length) {
                length = checked((int)position);
            }
            
            Commit();
        }

        public override void WriteByte(byte value) {
            ThrowIfDisposed();
            
            if (!writable) throw new NotSupportedException("Stream is not writable.");
            
            owner.OnWrite(fileName, temporary);
            
            EnsureCapacity(checked(position + 1));
            buffer[checked((int)position)] = value;
            position++;
            if (position > length) {
                length = checked((int)position);
            }
            
            Commit();
        }

        public override long Seek(long offset, SeekOrigin origin) {
            ThrowIfDisposed();
            
            long newPosition;
            switch (origin) {
                case SeekOrigin.Begin:
                    newPosition = offset;
                    break;
                case SeekOrigin.Current:
                    newPosition = checked(position + offset);
                    break;
                case SeekOrigin.End:
                    newPosition = checked(length + offset);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(origin));
            }

            if (newPosition < 0) {
                throw new IOException("Attempted to seek before the beginning of the stream.");
            }
            position = newPosition;
            return position;
        }

        public override void SetLength(long value) {
            ThrowIfDisposed();
            if (!writable) throw new NotSupportedException("Stream is not writable.");
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            
            EnsureCapacity(value);
            int newLength = checked((int)value);
            if (newLength > length) {
                Array.Clear(buffer, length, newLength - length);
            }

            length = newLength;
            if (position > value) {
                position = value;
            }
            
            Commit();
        }

        protected override void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing && writable) {
                    Commit();
                }

                disposed = true;
            }

            base.Dispose(disposing);
        }

        private void Commit() {
            byte[] data = new byte[length];
            if (length > 0) {
                Buffer.BlockCopy(buffer, 0, data, 0, length);
            }

            owner.Commit(fileName, data);
        }

        private void EnsureCapacity(long requiredLength) {
            if (requiredLength <= buffer.Length) return;
            int currentCapacity = buffer.Length;
            int newCapacity = currentCapacity == 0 ? InitialCapacity : currentCapacity;
            while (newCapacity < requiredLength) {
                int nextCapacity;
                if (newCapacity > int.MaxValue / 2) {
                    nextCapacity = int.MaxValue;
                } else {
                    nextCapacity = newCapacity * 2;
                }

                if (nextCapacity <= newCapacity) {
                    throw new IOException("Stream is too large.");
                }
                newCapacity = nextCapacity;
            }

            Array.Resize(ref buffer, newCapacity);
        }

        private static void ValidateBufferArguments(byte[] buffer, int offset, int count) {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            
            if (offset > buffer.Length - count) {
                throw new ArgumentException("Invalid offset/count combination.");
            }
        }

        private void ThrowIfDisposed() {
            if (disposed) {
                throw new ObjectDisposedException(nameof(ByteArrayStream));
            }
        }
    }
}