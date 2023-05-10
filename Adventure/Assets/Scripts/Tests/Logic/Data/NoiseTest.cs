using Adventure.Logic.Data;
using System.Collections.Generic;
using NUnit.Framework;

namespace Tests.Logic.Data {
    public class NoiseTest {
    
        [Test]
        public void RandomInt_SameSeedSameValue() {
            Noise noise1 = new Noise(123456789L);
            Noise noise2 = new Noise(123456789L);
            int rand1 = noise1.RandomInt(1);
            int rand2 = noise2.RandomInt(1);
            Assert.AreEqual(rand1, rand2, "The random numbers from the same seed should be equals");
        }
    
        [Test]
        public void RandomInt_RandomnessSpread() {
            Noise noise = new Noise(123456789L);
            HashSet<int> hash = new HashSet<int>();
            int collisions = 0;
            int min = int.MaxValue;
            for (int i = 0; i < 1000; i++) {
                int number = noise.RandomInt(i);
                if (!hash.Add(number)) {
                    collisions++;
                }
                if (number < min) min = number;
            }

            Assert.GreaterOrEqual(min, 0, "The minimum random value should be equal or greater than zero");
            Assert.LessOrEqual(collisions, 10, "The random collisions should not be more than 1% for small numbers");
        }
    
        [Test]
        public void Random_SameSeedSameValue() {
            Noise noise1 = new Noise(123456789L);
            Noise noise2 = new Noise(123456789L);
            double rand1 = noise1.Random(1);
            double rand2 = noise2.Random(1);
            Assert.AreEqual(rand1, rand2, 0, "The random numbers from the same seed should be equals");
        }
    
        [Test]
        public void Random_RandomnessSpread() {
            Noise noise = new Noise(123456789L);
            double min = double.MaxValue;
            double max = double.MinValue;
            var between = new int[10];
            for (int i = 0; i < 1000; i++) {
                double number = noise.Random(i);
                for (int j = 0; j < between.Length; j++) {
                    if (number >= j / (double) between.Length && number < (j + 1) / (double) between.Length) {
                        between[j]++;
                        break;
                    }
                }

                if (number < min) min = number;
                if (number > max) max = number;
            }

            Assert.GreaterOrEqual(min, 0, "The minimum random value should be equal or greater than zero");
            Assert.LessOrEqual(max, 1, "The maximum random value should be equal or less than one");
        
            for (int i = 0; i < between.Length; i++) {
                double spread = (between[i] / 1000.0d * between.Length);
                Assert.GreaterOrEqual(spread, 0.8, "The spread value should be more than 80% per decimal");
            }
        }
    
        [Test]
        public void RandomRange_SameSeedSameValue() {
            Noise noise1 = new Noise(123456789L);
            Noise noise2 = new Noise(123456789L);
            double rand1 = noise1.RandomRange(1, 0, 10);
            double rand2 = noise2.RandomRange(1, 0, 10);
            Assert.AreEqual(rand1, rand2, 0, "The random numbers from the same seed should be equals");
        }
    
        [Test]
        public void RandomRange_RandomnessSpread() {
            Noise noise = new Noise(123456789L);
            double min = double.MaxValue;
            double max = double.MinValue;
            float rangeMin = 1;
            float rangeMax = 100;
            float rangeDif = rangeMax - rangeMin;
            var between = new int[10];
            for (int i = 0; i < 1000; i++) {
                double number = noise.RandomRange(i, rangeMin, rangeMax);
                for (int j = 0; j < between.Length; j++) {
                    if (number >= j / (double) between.Length * rangeDif + rangeMin && 
                        number < (j + 1) / (double) between.Length * rangeDif + rangeMin) {
                        between[j]++;
                        break;
                    }
                }

                if (number < min) min = number;
                if (number > max) max = number;
            }

            Assert.GreaterOrEqual(min, rangeMin, "The minimum random value should be equal or greater than the minimum range");
            Assert.LessOrEqual(max, rangeMax, "The maximum random value should be equal or less than the maximum range");
        
            for (int i = 0; i < between.Length; i++) {
                double spread = (between[i] / 1000.0d * between.Length);
                Assert.GreaterOrEqual(spread, 0.8, "The spread value should be more than 80% per decimal");
            }
        }
    
        [Test]
        public void RandomRangeInt_SameSeedSameValue() {
            Noise noise1 = new Noise(123456789L);
            Noise noise2 = new Noise(123456789L);
            int rand1 = noise1.RandomRangeInt(1, 0, 10);
            int rand2 = noise2.RandomRangeInt(1, 0, 10);
            Assert.AreEqual(rand1, rand2, 0, "The random numbers from the same seed should be equals");
        }
    
        [Test]
        public void RandomRangeInt_RandomnessSpread() {
            Noise noise = new Noise(123456789L);
            int min = int.MaxValue;
            int max = int.MinValue;
            int rangeMin = 1;
            int rangeMax = 50;
            int rangeDif = rangeMax - rangeMin;
            var between = new int[10];
            for (int i = 0; i < 1000; i++) {
                int number = noise.RandomRangeInt(i, rangeMin, rangeMax);
                for (int j = 0; j < between.Length; j++) {
                    if (number >= j / (double) between.Length * rangeDif + rangeMin && 
                        number < (j + 1) / (double) between.Length * rangeDif + rangeMin) {
                        between[j]++;
                        break;
                    }
                }

                if (number < min) min = number;
                if (number > max) max = number;
            }

            Assert.AreEqual(min, rangeMin, "The minimum random value should be the minimum range at least one time");
            Assert.AreEqual(max, rangeMax, "The maximum random value should be the maximum range at least one time");
        
            for (int i = 0; i < between.Length; i++) {
                double spread = (between[i] / 1000.0d * between.Length);
                Assert.GreaterOrEqual(spread, 0.8, "The spread value should be more than 80% per decimal");
            }
        }
    
        [Test]
        public void Wave2D_SameSeedSameValue() {
            Noise noise1 = new Noise(123456789L);
            Noise noise2 = new Noise(123456789L);
            double rand1 = noise1.Wave2D(0.5f, 0.5f);
            double rand2 = noise2.Wave2D(0.5f, 0.5f);
            Assert.AreEqual(rand1, rand2, 0, "The wave values from the same seed and same position should be equals");
        }
    
        [Test]
        public void Wave2D_ValueRange() {
            Noise noise = new Noise(123456789L);
            double min = double.MaxValue;
            double max = double.MinValue;
            for (int i = 0; i < 1000; i++) {
                double number = noise.Wave2D(i % 10 * 0.333f, i / 10 * 0.333f);

                if (number < min) min = number;
                if (number > max) max = number;
            }
        
            Assert.GreaterOrEqual(min, -1, "The minimum noise value should be equal or greater than -1");
            Assert.LessOrEqual(max, 1, "The maximum random value should be equal or less than 1");
        }
    
        [Test]
        public void Wave3D_SameSeedSameValue() {
            Noise noise1 = new Noise(123456789L);
            Noise noise2 = new Noise(123456789L);
            double rand1 = noise1.Wave3D(0.5f, 0.5f, 0.5f);
            double rand2 = noise2.Wave3D(0.5f, 0.5f, 0.5f);
            Assert.AreEqual(rand1, rand2, 0, "The wave values from the same seed and same position should be equals");
        }
    
        [Test]
        public void Wave3D_ValueRange() {
            Noise noise = new Noise(123456789L);
            double min = double.MaxValue;
            double max = double.MinValue;
            for (int i = 0; i < 1000; i++) {
                double number = noise.Wave3D(i % 10 * 0.333f, i / 10 * 0.333f, i % 17 * 0.333f);

                if (number < min) min = number;
                if (number > max) max = number;
            }
        
            Assert.GreaterOrEqual(min, -1, "The minimum noise value should be equal or greater than -1");
            Assert.LessOrEqual(max, 1, "The maximum random value should be equal or less than 1");
        }
    }
}
