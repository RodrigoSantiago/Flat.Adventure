
using System;
using NUnit.Framework;
using Game.Data;

namespace Game.Tests.Data {
    [TestFixture]
    public class ChunkSoilPackedTests {
        private const int Size3D = ChunkSoil.Size3D;

        // ============================================================
        // Construction
        // ============================================================

        [Test]
        public void Constructor_Default_CreatesConstantDensityAndMaterial() {
            var chunk = new ChunkSoil();

            Assert.That(chunk.GetDensity(0, 0, 0), Is.EqualTo(0.0f));
            Assert.That(chunk.GetMaterial(0, 0, 0), Is.EqualTo(0));
        }

        [Test]
        public void Constructor_NullDensity_Throws() {
            Assert.Throws<ArgumentNullException>(() => new ChunkSoil(null, CreateConstantMaterial(0)));
        }

        [Test]
        public void Constructor_NullMaterial_Throws() {
            Assert.Throws<ArgumentNullException>(() => new ChunkSoil(CreateConstantDensity(0), null));
        }

        [Test]
        public void Constructor_TooShortDensity_Throws() {
            Assert.Throws<ArgumentException>(() => new ChunkSoil(new byte[] { 0 }, CreateConstantMaterial(0)));
        }

        [Test]
        public void Constructor_TooShortMaterial_Throws() {
            Assert.Throws<ArgumentException>(() => new ChunkSoil(CreateConstantDensity(0), new byte[] { 0 }));
        }

        [Test]
        public void Constructor_InvalidDensityMode_Throws() {
            var density = new byte[] { 5, 0 };
            Assert.Throws<ArgumentException>(() => new ChunkSoil(density, CreateConstantMaterial(0)));
        }

        [Test]
        public void Constructor_InvalidMaterialMode_Throws() {
            var material = new byte[] { 5, 0 };
            Assert.Throws<ArgumentException>(() => new ChunkSoil(CreateConstantDensity(0), material));
        }

        [Test]
        public void Constructor_DensityPacked1_IsRejected() {
            var density = CreatePackedDensity(ChunkSoil.Mode.Packed1);

            Assert.Throws<ArgumentException>(() => new ChunkSoil(density, CreateConstantMaterial(0)));
        }

        [Test]
        public void Constructor_DensityPacked2_IsRejected() {
            var density = CreatePackedDensity(ChunkSoil.Mode.Packed2);

            Assert.Throws<ArgumentException>(() => new ChunkSoil(density, CreateConstantMaterial(0)));
        }

        [Test]
        public void Constructor_DensityPacked6_IsRejected() {
            var density = CreatePackedDensity(ChunkSoil.Mode.Packed6);

            Assert.Throws<ArgumentException>(() => new ChunkSoil(density, CreateConstantMaterial(0)));
        }

        [Test]
        public void Constructor_MaterialPaletteLargerThanCapacity_Throws() {
            var material = CreatePackedMaterial(ChunkSoil.Mode.Packed1);
            material[1] = 3;

            Assert.Throws<ArgumentException>(() => new ChunkSoil(CreateConstantDensity(0), material));
        }

        [Test]
        public void Constructor_ValidPacked4Density_IsAccepted() {
            var density = CreatePackedDensity(ChunkSoil.Mode.Packed4);
            var chunk = new ChunkSoil(density, CreateConstantMaterial(0));

            Assert.That(chunk.GetDensity(0, 0, 0), Is.EqualTo(0.0f));
        }

        // ============================================================
        // Coordinates
        // ============================================================

        [TestCase(-1, 0, 0)]
        [TestCase(32, 0, 0)]
        [TestCase(0, -1, 0)]
        [TestCase(0, 32, 0)]
        [TestCase(0, 0, -1)]
        [TestCase(0, 0, 32)]
        [TestCase(int.MinValue, 0, 0)]
        [TestCase(int.MaxValue, 0, 0)]
        public void GetDensity_InvalidCoordinates_Throws(int x, int y, int z) {
            var chunk = new ChunkSoil();

            Assert.Throws<ArgumentOutOfRangeException>(() => chunk.GetDensity(x, y, z));
        }

        [TestCase(-1, 0, 0)]
        [TestCase(32, 0, 0)]
        [TestCase(0, -1, 0)]
        [TestCase(0, 32, 0)]
        [TestCase(0, 0, -1)]
        [TestCase(0, 0, 32)]
        public void GetMaterial_InvalidCoordinates_Throws(int x, int y, int z) {
            var chunk = new ChunkSoil();

            Assert.Throws<ArgumentOutOfRangeException>(() => chunk.GetMaterial(x, y, z));
        }

        [Test]
        public void AllEightChunkCorners_AreAccessible() {
            var chunk = new ChunkSoil();

            var coordinates = new[] {
                (0, 0, 0),
                (31, 0, 0),
                (0, 31, 0),
                (31, 31, 0),
                (0, 0, 31),
                (31, 0, 31),
                (0, 31, 31),
                (31, 31, 31)
            };

            foreach (var (x, y, z) in coordinates) {
                Assert.That(chunk.GetDensity(x, y, z), Is.EqualTo(0.0f));
                Assert.That(chunk.GetMaterial(x, y, z), Is.EqualTo(0));
            }
        }

        // ============================================================
        // Material boundaries
        // ============================================================

        [TestCase(-1)]
        [TestCase(64)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void SetMaterial_OutOfRange_Throws(int value) {
            var chunk = new ChunkSoil();

            Assert.Throws<ArgumentOutOfRangeException>(() => chunk.SetMaterial(0, 0, 0, value));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(31)]
        [TestCase(32)]
        [TestCase(62)]
        [TestCase(63)]
        public void SetMaterial_AllValidValues_RoundTrip(int value) {
            var chunk = new ChunkSoil();

            chunk.SetMaterial(0, 0, 0, value);

            Assert.That(chunk.GetMaterial(0, 0, 0), Is.EqualTo(value));
        }

        // ============================================================
        // Density quantization
        // ============================================================

        [TestCase(0.00f, 0.00f)]
        [TestCase(0.10f, 0.10f)]
        [TestCase(0.20f, 0.20f)]
        [TestCase(0.30f, 0.30f)]
        [TestCase(0.40f, 0.40f)]
        [TestCase(0.55f, 0.55f)]
        [TestCase(0.60f, 0.60f)]
        [TestCase(0.65f, 0.65f)]
        [TestCase(0.70f, 0.70f)]
        [TestCase(0.75f, 0.75f)]
        [TestCase(0.80f, 0.80f)]
        [TestCase(0.85f, 0.85f)]
        [TestCase(0.90f, 0.90f)]
        [TestCase(0.95f, 0.95f)]
        [TestCase(1.00f, 1.00f)]
        [TestCase(2.00f, 2.00f)]
        public void SetDensity_CubeDensityValues_RoundTrip(float input, float expected) {
            var chunk = new ChunkSoil();

            chunk.SetDensity(0, 0, 0, input);
            Assert.That(chunk.GetDensity(0, 0, 0), Is.EqualTo(expected));
        }

        [Test]
        public void SetDensity_ChangesConstantChunkToPacked4() {
            var chunk = new ChunkSoil();
            Assert.That(chunk.DensityBitSize, Is.EqualTo(ChunkSoil.Mode.Constant));

            chunk.SetDensity(0, 0, 0, 0.55f);
            Assert.That(chunk.DensityBitSize, Is.EqualTo(ChunkSoil.Mode.Packed4));
            
            Assert.That(chunk.GetDensity(0, 0, 0), Is.EqualTo(0.55f));
            Assert.That(chunk.GetDensity(1, 0, 0), Is.EqualTo(0.0f));
        }

        [Test]
        public void SetDensity_SameValue_DoesNotNeedExpansion() {
            var chunk = new ChunkSoil();
            Assert.That(chunk.DensityBitSize, Is.EqualTo(ChunkSoil.Mode.Constant));

            chunk.SetDensity(0, 0, 0, 0.0f);
            chunk.SetDensity(0, 0, 0, 0.0f);
            Assert.That(chunk.DensityBitSize, Is.EqualTo(ChunkSoil.Mode.Constant));

            Assert.That(chunk.GetDensity(0, 0, 0), Is.EqualTo(0.0f));
        }

        [Test]
        public void SetDensity_AllSixteenDensityValues() {
            var chunk = new ChunkSoil();
            Assert.That(chunk.DensityBitSize, Is.EqualTo(ChunkSoil.Mode.Constant));

            for (int i = 0; i < ChunkSoil.CubeDensity.Length; i++) {
                int x = i % 16;
                int z = i / 16;

                chunk.SetDensity(x, 0, z, ChunkSoil.CubeDensity[i]);
            }
            Assert.That(chunk.DensityBitSize, Is.EqualTo(ChunkSoil.Mode.Packed4));

            for (int i = 0; i < ChunkSoil.CubeDensity.Length; i++) {
                int x = i % 16;
                int z = i / 16;

                Assert.That(chunk.GetDensity(x, 0, z), Is.EqualTo(ChunkSoil.CubeDensity[i]));
            }
        }

        // ============================================================
        // Density expansion / compaction
        // ============================================================

        [Test]
        public void ExpandDensity_ConstantValue_IsReplicatedToEveryVoxel() {
            var chunk = new ChunkSoil();

            chunk.SetDensity(0, 0, 0, 0.75f);

            var source = new ChunkSoil(CreateConstantDensity(9), CreateConstantMaterial(0));
            Assert.That(source.DensityBitSize, Is.EqualTo(ChunkSoil.Mode.Constant));

            source.ExpandDensity();
            Assert.That(source.DensityBitSize, Is.EqualTo(ChunkSoil.Mode.Packed4));

            for (int i = 0; i < Size3D; i++) {
                Coordinates(i, out int x, out int y, out int z);

                Assert.That(source.GetDensity(x, y, z), Is.EqualTo(ChunkSoil.CubeDensity[9]));
            }
        }

        [Test]
        public void ExpandDensity_WhenAlreadyPacked4_DoesNothing() {
            var density = CreatePackedDensity(ChunkSoil.Mode.Packed4);

            var chunk = new ChunkSoil(density, CreateConstantMaterial(0));
            Assert.That(chunk.DensityBitSize, Is.EqualTo(ChunkSoil.Mode.Packed4));

            chunk.SetDensity(31, 31, 31, 1.0f);
            Assert.That(chunk.DensityBitSize, Is.EqualTo(ChunkSoil.Mode.Packed4));
            
            float valueBefore = chunk.GetDensity(31, 31, 31);

            chunk.ExpandDensity();
            Assert.That(chunk.DensityBitSize, Is.EqualTo(ChunkSoil.Mode.Packed4));

            Assert.That(chunk.GetDensity(31, 31, 31), Is.EqualTo(valueBefore));
        }

        [Test]
        public void CompactDensity_ConstantPackedData_BecomesConstant() {
            var density = CreatePackedDensity(ChunkSoil.Mode.Packed4);

            var chunk = new ChunkSoil(density, CreateConstantMaterial(0));
            Assert.That(chunk.DensityBitSize, Is.EqualTo(ChunkSoil.Mode.Packed4));

            for (int i = 0; i < Size3D; i++) {
                WriteBits(density, 1, ChunkSoil.Mode.Packed4, i, 7);
            }
            Assert.That(chunk.DensityBitSize, Is.EqualTo(ChunkSoil.Mode.Packed4));

            chunk.CompactDensity();
            Assert.That(chunk.DensityBitSize, Is.EqualTo(ChunkSoil.Mode.Constant));

            for (int i = 0; i < Size3D; i++) {
                Coordinates(i, out int x, out int y, out int z);

                Assert.That(chunk.GetDensity(x, y, z), Is.EqualTo(ChunkSoil.CubeDensity[7]));
            }
        }

        [Test]
        public void CompactDensity_NonConstantData_RemainsReadable() {
            var density = CreatePackedDensity(ChunkSoil.Mode.Packed4);

            var chunk = new ChunkSoil(density, CreateConstantMaterial(0));
            Assert.That(chunk.DensityBitSize, Is.EqualTo(ChunkSoil.Mode.Packed4));

            chunk.SetDensity(0, 0, 0, 0.0f);
            chunk.SetDensity(1, 0, 0, 1.0f);
            Assert.That(chunk.DensityBitSize, Is.EqualTo(ChunkSoil.Mode.Packed4));

            chunk.CompactDensity();
            Assert.That(chunk.DensityBitSize, Is.EqualTo(ChunkSoil.Mode.Packed4));

            Assert.That(chunk.GetDensity(0, 0, 0), Is.EqualTo(0.0f));
            Assert.That(chunk.GetDensity(1, 0, 0), Is.EqualTo(1.0f));
        }

        // ============================================================
        // Material expansion
        // ============================================================

        [Test]
        public void ExpandMaterial_ConstantToPacked1_PreservesValue() {
            var chunk = new ChunkSoil(CreateConstantDensity(0), CreateConstantMaterial(42));
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Constant));

            chunk.ExpandMaterial();
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed1));

            Assert.That(chunk.GetMaterial(0, 0, 0), Is.EqualTo(42));
            Assert.That(chunk.GetMaterial(31, 31, 31), Is.EqualTo(42));
        }

        [Test]
        public void ExpandMaterial_ConstantToPacked2_PreservesValue() {
            var chunk = new ChunkSoil(CreateConstantDensity(0), CreateConstantMaterial(42));
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Constant));

            chunk.ExpandMaterial(3);
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed2));

            Assert.That(chunk.GetMaterial(0, 0, 0), Is.EqualTo(42));
            Assert.That(chunk.GetMaterial(31, 31, 31), Is.EqualTo(42));
        }

        [Test]
        public void ExpandMaterial_ConstantToPacked4_PreservesValue() {
            var chunk = new ChunkSoil(CreateConstantDensity(0), CreateConstantMaterial(42));
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Constant));

            chunk.ExpandMaterial(10);
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed4));

            Assert.That(chunk.GetMaterial(0, 0, 0), Is.EqualTo(42));
            Assert.That(chunk.GetMaterial(31, 31, 31), Is.EqualTo(42));
        }

        [Test]
        public void ExpandMaterial_ConstantToPacked6_PreservesValue() {
            var chunk = new ChunkSoil(CreateConstantDensity(0), CreateConstantMaterial(42));
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Constant));

            chunk.ExpandMaterial(17);
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed6));

            Assert.That(chunk.GetMaterial(0, 0, 0), Is.EqualTo(42));
            Assert.That(chunk.GetMaterial(31, 31, 31), Is.EqualTo(42));
        }

        [Test]
        public void ExpandMaterial_AlreadyPacked6_DoesNothing() {
            var material = CreatePackedMaterial(ChunkSoil.Mode.Packed6);

            for (int i = 0; i < Size3D; i++) {
                WriteBits(material, 1, ChunkSoil.Mode.Packed6, i, i % 64);
            }

            var chunk = new ChunkSoil(CreateConstantDensity(0), material);
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed6));

            chunk.ExpandMaterial();
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed6));

            for (int i = 0; i < Size3D; i ++) {
                Coordinates(i, out int x, out int y, out int z);

                Assert.That(chunk.GetMaterial(x, y, z), Is.EqualTo(i % 64));
            }
        }

        [Test]
        public void ExpandMaterial_FromPacked1ToPacked2_PreservesValues() {
            var chunk = new ChunkSoil();
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Constant));

            chunk.SetMaterial(1, 0, 0, 20);
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed1));

            chunk.ExpandMaterial(3);
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed2));

            Assert.That(chunk.GetMaterial(0, 0, 0), Is.EqualTo(0));
            Assert.That(chunk.GetMaterial(1, 0, 0), Is.EqualTo(20));
        }

        [Test]
        public void ExpandMaterial_FromPacked2ToPacked4_PreservesValues() {
            var chunk = new ChunkSoil();
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Constant));

            chunk.SetMaterial(0, 0, 0, 10);
            chunk.SetMaterial(1, 0, 0, 20);
            chunk.SetMaterial(2, 0, 0, 30);
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed2));

            chunk.ExpandMaterial(5);
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed4));

            Assert.That(chunk.GetMaterial(0, 0, 0), Is.EqualTo(10));
            Assert.That(chunk.GetMaterial(1, 0, 0), Is.EqualTo(20));
            Assert.That(chunk.GetMaterial(2, 0, 0), Is.EqualTo(30));
        }

        [Test]
        public void ExpandMaterial_FromPacked4ToPacked6_PreservesValues() {
            var chunk = new ChunkSoil();

            for (int i = 0; i < Size3D; i++) {
                Coordinates(i, out int x, out int y, out int z);
                chunk.SetMaterial(x, y, z, i % 16);
            }
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed4));

            chunk.ExpandMaterial(17);
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed6));
            
            for (int i = 0; i < Size3D; i++) {
                Coordinates(i, out int x, out int y, out int z);

                Assert.That(chunk.GetMaterial(x, y, z), Is.EqualTo(i % 16));
            }
        }

        // ============================================================
        // Automatic material mode transitions
        // ============================================================

        [Test]
        public void SetMaterial_TwoValues_UsesPacked1Semantics() {
            var chunk = new ChunkSoil();

            chunk.SetMaterial(1, 0, 0, 20);
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed1));

            Assert.That(chunk.GetMaterial(0, 0, 0), Is.EqualTo(0));
            Assert.That(chunk.GetMaterial(1, 0, 0), Is.EqualTo(20));
        }

        [Test]
        public void SetMaterial_ThreeValues_ExpandsAndPreservesExistingValues() {
            var chunk = new ChunkSoil();

            chunk.SetMaterial(0, 0, 0, 10);
            chunk.SetMaterial(1, 0, 0, 20);
            chunk.SetMaterial(2, 0, 0, 30);
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed2));

            Assert.That(chunk.GetMaterial(0, 0, 0), Is.EqualTo(10));
            Assert.That(chunk.GetMaterial(1, 0, 0), Is.EqualTo(20));
            Assert.That(chunk.GetMaterial(2, 0, 0), Is.EqualTo(30));
        }

        [Test]
        public void SetMaterial_FiveValues_ExpandsToPacked4Semantics() {
            var chunk = new ChunkSoil();

            for (int i = 0; i < Size3D; i++) {
                Coordinates(i, out int x, out int y, out int z);
                chunk.SetMaterial(x, y, z, i % 5);
            }
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed4));

            for (int i = 0; i < Size3D; i++) {
                Coordinates(i, out int x, out int y, out int z);
                Assert.That(chunk.GetMaterial(x, y, z), Is.EqualTo(i % 5));
            }
        }

        [Test]
        public void SetMaterial_SeventeenUniqueValues_ExpandsToPacked6() {
            var chunk = new ChunkSoil();

            for (int i = 0; i < Size3D; i++) {
                Coordinates(i, out int x, out int y, out int z);
                chunk.SetMaterial(x, y, z, i % 17);
            }
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed6));

            for (int i = 0; i < Size3D; i++) {
                Coordinates(i, out int x, out int y, out int z);
                Assert.That(chunk.GetMaterial(x, y, z), Is.EqualTo(i % 17));
            }
        }

        [Test]
        public void SetMaterial_All64Values_RoundTrip() {
            var chunk = new ChunkSoil();

            for (int i = 0; i < Size3D; i++) {
                Coordinates(i, out int x, out int y, out int z);
                chunk.SetMaterial(x, y, z, i % 64);
            }
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed6));

            for (int i = 0; i < Size3D; i++) {
                Coordinates(i, out int x, out int y, out int z);
                Assert.That(chunk.GetMaterial(x, y, z), Is.EqualTo(i % 64));
            }
        }

        // ============================================================
        // Material compaction
        // ============================================================

        [Test]
        public void CompactMaterial_OneValue_BecomesConstant() {
            var chunk = CreatePacked6ChunkWithPattern(1);
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed6));

            chunk.CompactMaterial();
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Constant));

            for (int i = 0; i < Size3D; i++) {
                Coordinates(i, out int x, out int y, out int z);

                Assert.That(chunk.GetMaterial(x, y, z), Is.EqualTo(7));
            }
        }

        [Test]
        public void CompactMaterial_TwoValues_BecomesPacked1() {
            var chunk = CreatePacked6ChunkWithPattern(2);
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed6));

            chunk.CompactMaterial();
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed1));

            Assert.That(chunk.GetMaterial(0, 0, 0), Is.EqualTo(10));
            Assert.That(chunk.GetMaterial(1, 0, 0), Is.EqualTo(11));

            for (int i = 0; i < Size3D; i++) {
                Coordinates(i, out int x, out int y, out int z);

                Assert.That(chunk.GetMaterial(x, y, z), Is.EqualTo(10 + i % 2));
            }
        }

        [TestCase(3)]
        [TestCase(4)]
        public void CompactMaterial_Values_BecomesPacked2(int values) {
            var chunk = CreatePacked6ChunkWithPattern(values);
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed6));

            chunk.CompactMaterial();
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed2));

            for (int i = 0; i < Size3D; i++) {
                Coordinates(i, out int x, out int y, out int z);

                Assert.That(chunk.GetMaterial(x, y, z), Is.EqualTo(10 + i % values));
            }
        }

        [TestCase(5)]
        [TestCase(6)]
        [TestCase(10)]
        [TestCase(16)]
        public void CompactMaterial_Values_BecomesPacked4(int values) {
            var chunk = CreatePacked6ChunkWithPattern(values);
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed6));

            for (int i = 0; i < Size3D; i++) {
                Coordinates(i, out int x, out int y, out int z);

                Assert.That(chunk.GetMaterial(x, y, z), Is.EqualTo(10 + i % values));
            }

            chunk.CompactMaterial();
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed4));

            for (int i = 0; i < Size3D; i++) {
                Coordinates(i, out int x, out int y, out int z);

                Assert.That(chunk.GetMaterial(x, y, z), Is.EqualTo(10 + i % values));
            }
        }

        [Test]
        public void CompactMaterial_SeventeenValues_DoesNotCompactToPacked4() {
            var chunk = CreatePacked6ChunkWithPattern(17);
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed6));

            chunk.CompactMaterial();
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed6));

            for (int i = 0; i < Size3D; i++) {
                Coordinates(i, out int x, out int y, out int z);

                Assert.That(chunk.GetMaterial(x, y, z), Is.EqualTo(10 + i % 17));
            }
        }

        [Test]
        public void CompactMaterial_ThirtyTwoValues_DoesNotCompactToPacked4() {
            var chunk = CreatePacked6ChunkWithPattern(32);
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed6));

            chunk.CompactMaterial();
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed6));

            for (int i = 0; i < Size3D; i++) {
                Coordinates(i, out int x, out int y, out int z);

                Assert.That(chunk.GetMaterial(x, y, z), Is.EqualTo(10 + i % 32));
            }
        }

        [Test]
        public void CompactMaterial_Packed4WithEightValues_RemainsReadable() {
            var chunk = new ChunkSoil();
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Constant));

            for (int i = 0; i < Size3D; i++) {
                Coordinates(i, out int x, out int y, out int z);
                chunk.SetMaterial(x, y, z, i % 8);
            }
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed4));

            chunk.ExpandMaterial(17);
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed6));
            
            chunk.CompactMaterial();
            Assert.That(chunk.MaterialBitSize, Is.EqualTo(ChunkSoil.Mode.Packed4));

            for (int i = 0; i < Size3D; i++) {
                Coordinates(i, out int x, out int y, out int z);

                Assert.That(chunk.GetMaterial(x, y, z), Is.EqualTo(i % 8));
            }
        }

        // ============================================================
        // Packed bit operations
        // ============================================================

        [Test]
        public void Mode_Packed1_ReadWrite_AllBitPositions() {
            var data = new byte[ChunkSoil.Mode.Packed1.dataSize + 4];
            var mode = ChunkSoil.Mode.Packed1;

            for (int i = 0; i < Size3D; i++) {
                mode.WriteBits(data, 1, i, i & 1);
            }

            for (int i = 0; i < Size3D; i++) {
                Assert.That(mode.ReadBits(data, 1, i), Is.EqualTo(i & 1));
            }
        }

        [Test]
        public void Mode_Packed2_ReadWrite_AllValues() {
            var data = new byte[ChunkSoil.Mode.Packed2.dataSize + 4];
            var mode = ChunkSoil.Mode.Packed2;

            for (int i = 0; i < Size3D; i++) {
                mode.WriteBits(data, 1, i, i & 3);
            }

            for (int i = 0; i < Size3D; i++) {
                Assert.That(mode.ReadBits(data, 1, i), Is.EqualTo(i & 3));
            }
        }

        [Test]
        public void Mode_Packed4_ReadWrite_AllValues() {
            var data = new byte[ChunkSoil.Mode.Packed4.dataSize + 4];
            var mode = ChunkSoil.Mode.Packed4;

            for (int i = 0; i < Size3D; i++) {
                mode.WriteBits(data, 1, i, i & 15);
            }

            for (int i = 0; i < Size3D; i++) {
                Assert.That(mode.ReadBits(data, 1, i), Is.EqualTo(i & 15));
            }
        }

        [Test]
        public void Mode_Packed6_ReadWrite_AllValues() {
            var data = new byte[ChunkSoil.Mode.Packed6.dataSize + 4];
            var mode = ChunkSoil.Mode.Packed6;

            for (int i = 0; i < Size3D; i++) {
                mode.WriteBits(data, 1, i, i & 63);
            }

            for (int i = 0; i < Size3D; i++) {
                Assert.That(mode.ReadBits(data, 1, i), Is.EqualTo(i & 63));
            }
        }

        [Test]
        public void Mode_Packed6_ReadWrite_CrossByteBoundaries() {
            var data = new byte[ChunkSoil.Mode.Packed6.dataSize + 4];
            var mode = ChunkSoil.Mode.Packed6;

            int[] indices = {
                0, 1, 2, 3, 4, 5,
                7, 8, 9, 10,
                15, 16, 17,
                31, 32, 33,
                63, 64, 65,
                127, 128, 129,
                Size3D - 3,
                Size3D - 2,
                Size3D - 1
            };

            foreach (int index in indices) {
                int value = (index * 37) & 63;

                mode.WriteBits(data, 1, index, value);

                Assert.That(mode.ReadBits(data, 1, index), Is.EqualTo(value), $"Failed at index {index}");
            }
        }

        [Test]
        public void Mode_WriteBits_OverwritesExistingPacked4Value() {
            var data = new byte[ChunkSoil.Mode.Packed4.dataSize + 4];
            var mode = ChunkSoil.Mode.Packed4;

            mode.WriteBits(data, 1, 0, 15);
            mode.WriteBits(data, 1, 0, 3);

            Assert.That(mode.ReadBits(data, 1, 0), Is.EqualTo(3));
        }

        [Test]
        public void Mode_WriteBits_OverwritesExistingPacked6Value() {
            var data = new byte[ChunkSoil.Mode.Packed6.dataSize + 4];
            var mode = ChunkSoil.Mode.Packed6;

            mode.WriteBits(data, 1, 1, 63);
            mode.WriteBits(data, 1, 1, 4);

            Assert.That(mode.ReadBits(data, 1, 1), Is.EqualTo(4));
        }

        [Test]
        public void Mode_WriteBits_ValueOutsideCapacity_IsMasked() {
            var data = new byte[ChunkSoil.Mode.Packed4.dataSize + 4];
            var mode = ChunkSoil.Mode.Packed4;

            mode.WriteBits(data, 1, 0, 0xFF);

            Assert.That(mode.ReadBits(data, 1, 0), Is.EqualTo(15));
        }

        // ============================================================
        // Mode metadata
        // ============================================================

        [Test]
        public void Mode_DataSizes_AreCorrect() {
            Assert.That(ChunkSoil.Mode.Constant.dataSize, Is.EqualTo(0));

            Assert.That(ChunkSoil.Mode.Packed1.dataSize, Is.EqualTo(4096));

            Assert.That(ChunkSoil.Mode.Packed2.dataSize, Is.EqualTo(8192));

            Assert.That(ChunkSoil.Mode.Packed4.dataSize, Is.EqualTo(16384));

            Assert.That(ChunkSoil.Mode.Packed6.dataSize, Is.EqualTo(24576));
        }

        // ============================================================
        // Unsafe access
        // ============================================================

        [Test]
        public void UnsafeGetDensity_ValidCoordinates_MatchesSafeAccess() {
            var chunk = new ChunkSoil();

            chunk.SetDensity(3, 7, 11, 0.85f);

            Assert.That(chunk.UnsafeGetDensity(3, 7, 11), Is.EqualTo(chunk.GetDensity(3, 7, 11)));
        }

        // ============================================================
        // Extreme / suspicious inputs
        // ============================================================

        [Test]
        public void SetDensity_ValueAboveOne_MapsToSpecialDensity() {
            var chunk = new ChunkSoil();

            chunk.SetDensity(0, 0, 0, 1.01f);

            Assert.That(chunk.GetDensity(0, 0, 0), Is.EqualTo(2.0f));
        }

        [Test]
        public void SetDensity_Two_MapsToSpecialDensity() {
            var chunk = new ChunkSoil();

            chunk.SetDensity(0, 0, 0, 2.0f);

            Assert.That(chunk.GetDensity(0, 0, 0), Is.EqualTo(2.0f));
        }

        [Test]
        public void SetDensity_LargePositiveValue_MapsToSpecialDensity() {
            var chunk = new ChunkSoil();

            chunk.SetDensity(0, 0, 0, float.MaxValue);

            Assert.That(chunk.GetDensity(0, 0, 0), Is.EqualTo(2.0f));
        }

        [Test]
        public void SetDensity_NaN_CurrentImplementationMapsToSpecialDensity() {
            var chunk = new ChunkSoil();

            chunk.SetDensity(0, 0, 0, float.NaN);

            Assert.That(chunk.GetDensity(0, 0, 0), Is.EqualTo(0.0f));
        }

        [Test]
        public void SetDensity_NegativeValue_CurrentImplementationDoesNotClamp() {
            var chunk = new ChunkSoil();

            chunk.SetDensity(0, 0, 0, -1.0f);

            Assert.That(chunk.GetDensity(0, 0, 0), Is.EqualTo(0.0f));
        }

        // ============================================================
        // Full round-trip / stress tests
        // ============================================================

        [Test]
        public void FullChunk_DensityPattern_RoundTrips() {
            var chunk = new ChunkSoil();

            for (int i = 0; i < Size3D; i++) {
                Coordinates(i, out int x, out int y, out int z);

                int densityIndex = i & 15;

                chunk.SetDensity(x, y, z, ChunkSoil.CubeDensity[densityIndex]);
            }

            for (int i = 0; i < Size3D; i++) {
                Coordinates(i, out int x, out int y, out int z);

                int expected = i & 15;

                Assert.That(chunk.GetDensity(x, y, z),
                    Is.EqualTo(ChunkSoil.CubeDensity[expected]), $"Density mismatch at voxel {i}");
            }
        }

        [Test]
        public void FullChunk_MaterialPattern_RoundTrips() {
            var chunk = new ChunkSoil();

            for (int i = 0; i < Size3D; i++) {
                Coordinates(i, out int x, out int y, out int z);

                chunk.SetMaterial(x, y, z, i & 63);
            }

            for (int i = 0; i < Size3D; i++) {
                Coordinates(i, out int x, out int y, out int z);

                Assert.That(chunk.GetMaterial(x, y, z),
                    Is.EqualTo(i & 63), $"Material mismatch at voxel {i}");
            }
        }

        [Test]
        public void FullChunk_MaterialExpandThenCompact_PreservesData() {
            var chunk = new ChunkSoil();

            // Eight materials are enough to force Packed4,
            // while still allowing compaction back to Packed4.
            for (int i = 0; i < Size3D; i++) {
                Coordinates(i, out int x, out int y, out int z);

                chunk.SetMaterial(x, y, z, (i % 8) + 10);
            }

            chunk.ExpandMaterial(17);
            chunk.CompactMaterial();

            for (int i = 0; i < Size3D; i++) {
                Coordinates(i, out int x, out int y, out int z);

                Assert.That(chunk.GetMaterial(x, y, z),
                    Is.EqualTo((i % 8) + 10), $"Material mismatch at voxel {i}");
            }
        }

        // ============================================================
        // Helpers
        // ============================================================

        private static byte[] CreateConstantDensity(int value) {
            var data = new byte[4];
            data[1] = (byte)value;
            return data;
        }

        private static byte[] CreateConstantMaterial(int value) {
            var data = new byte[4];
            data[1] = (byte)value;
            return data;
        }

        private static byte[] CreatePackedDensity(ChunkSoil.Mode mode) {
            var data = new byte[mode.DenArraySize];
            data[0] = mode.id;
            return data;
        }

        private static byte[] CreatePackedMaterial(ChunkSoil.Mode mode) {
            byte[] data;
            if (mode == ChunkSoil.Mode.Constant) {
                data = new byte[mode.MatArraySize];
            } else if (mode == ChunkSoil.Mode.Packed6) {
                data = new byte[mode.MatArraySize];
            } else {
                data = new byte[mode.MatArraySize];
                data[1] = 1; // count = 1, minimum
            }

            data[0] = mode.id;
            return data;
        }

        private static void WriteBits(byte[] data, int offset, ChunkSoil.Mode mode, int index, int value) {
            mode.WriteBits(data, offset, index, value);
        }

        private static ChunkSoil CreatePacked6ChunkWithPattern(int distinctValues) {
            var material = CreatePackedMaterial(ChunkSoil.Mode.Packed6);

            for (int i = 0; i < Size3D; i++) {
                int value;

                if (distinctValues == 1) {
                    value = 7;
                } else {
                    value = 10 + (i % distinctValues);
                }

                WriteBits(material, 1, ChunkSoil.Mode.Packed6, i, value);
            }

            return new ChunkSoil(CreateConstantDensity(0), material);
        }

        private static void Coordinates(int index, out int x, out int y, out int z) {
            x = index % ChunkSoil.Size1D;

            int yz = index / ChunkSoil.Size1D;

            z = yz % ChunkSoil.Size1D;
            y = yz / ChunkSoil.Size1D;
        }
    }
}