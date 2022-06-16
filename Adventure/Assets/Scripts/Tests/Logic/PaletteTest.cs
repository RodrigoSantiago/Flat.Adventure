using System;
using System.Collections;
using System.Collections.Generic;
using Adventure.Logic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Logic {
    public class PaletteTest {
        
        [Test]
        public void HappyDay() {
            short[] readTypes = {1, 2, 3, 4, 5};
            Palette palette = new Palette(false, readTypes, 5);

            AssertPalette(palette, 3, 5, 0b111110L, false);
            AssertRegisteredTypes(palette, 1, 2, 3, 4, 5);
        }
        
        [Test]
        public void EmptyPalette() {
            short[] readTypes = {};
            Palette palette = new Palette(false, readTypes, 0);
            
            AssertPalette(palette, 2, 0, 0L, false);
            AssertRegisteredTypes(palette, 0);
        }
        
        [Test]
        public void Contains() {
            short[] readTypes = {1, 2, 3, 4, 5};
            Palette palette = new Palette(false, readTypes, 5);

            AssertPalette(palette, 3, 5, 0b111110L, false);
            AssertRegisteredTypes(palette, 1, 2, 3, 4, 5);
            
            Assert.IsFalse(palette.Contains(0), "Incorrect (0) value found");
            Assert.IsTrue(palette.Contains(1), "Value (1) not found");
            Assert.IsTrue(palette.Contains(2), "Value (2) not found");
            Assert.IsTrue(palette.Contains(3), "Value (3) not found");
            Assert.IsTrue(palette.Contains(4), "Value (4) not found");
            Assert.IsTrue(palette.Contains(5), "Value (5) not found");
            Assert.IsFalse(palette.Contains(6), "Incorrect (6) value found");
        }
        
        [Test]
        public void Contains_BitCollision() {
            short[] readTypes = {65, 2, 3, 4, 5};
            Palette palette = new Palette(false, readTypes, 5);

            AssertPalette(palette, 3, 5, 0b111110L, false);
            AssertRegisteredTypes(palette, 65, 2, 3, 4, 5);
            
            Assert.IsFalse(palette.Contains(0), "Incorrect (0) value found");
            Assert.IsFalse(palette.Contains(1), "Incorrect (1) value found");
            Assert.IsTrue(palette.Contains(2), "Value (2) not found");
            Assert.IsTrue(palette.Contains(3), "Value (3) not found");
            Assert.IsTrue(palette.Contains(4), "Value (4) not found");
            Assert.IsTrue(palette.Contains(5), "Value (5) not found");
            Assert.IsFalse(palette.Contains(6), "Incorrect (6) value found");
            Assert.IsTrue(palette.Contains(65), "Value (65) not found");
        }
        
        [Test]
        public void Add_NewType() {
            short[] readTypes = {1, 2, 3, 4, 5};
            Palette paletteBefore = new Palette(false, readTypes, 5);
            Palette palette = paletteBefore.Add(6);
            
            Assert.AreEqual(paletteBefore, palette, "The Palette should be the same");
            AssertPalette(palette, 3, 6, 0b1111110L, false);
            AssertRegisteredTypes(palette, 1, 2, 3, 4, 5, 6);
        }
        
        [Test]
        public void Add_RedisteredType() {
            short[] readTypes = {1, 2, 3, 4, 5};
            Palette paletteBefore = new Palette(false, readTypes, 5);
            Palette palette = paletteBefore.Add(5);
            
            Assert.AreEqual(paletteBefore, palette, "The Palette should be the same");
            AssertPalette(palette, 3, 5, 0b111110L, false);
            AssertRegisteredTypes(palette, 1, 2, 3, 4, 5, 0);
        }
        
        [Test]
        public void Add_NewTypeToCommon() {
            short[] readTypes = {1, 2, 3, 4, 5};
            Palette paletteBefore = new Palette(true, readTypes, 5);
            Palette palette = paletteBefore.Add(6);
            
            Assert.AreNotEqual(paletteBefore, palette, "The Palette should not be the same");
            AssertPalette(palette, 3, 6, 0b1111110L, false);
            AssertRegisteredTypes(palette, 1, 2, 3, 4, 5, 6);
        }
        
        [Test]
        public void Add_Overflow() {
            short[] readTypes = new short[256];
            for (int i = 0; i < 256; i++) {
                readTypes[i] = (short) (i + 1);
            }
            Palette paletteBefore = new Palette(false, readTypes, 256);
            Palette palette = paletteBefore.Add(257);
            
            Assert.Null(palette, "The Palette should not be created");
        }

        private void AssertPalette(Palette palette, int bits, int size, long signature, bool common) {
            Assert.AreEqual(bits, palette.Bits, "The used Bits should be equal to the minimum needed to reference all values");
            Assert.AreEqual(size, palette.Size, "The Size should be equal to the registered types");
            Assert.AreEqual(signature, palette.Signature, "The Signature should fill the bits wrapping on 64");
            Assert.AreEqual(common, palette.IsCommon);
        }

        private void AssertRegisteredTypes(Palette palette, params short[] types) {
            for (int i = 0; i < types.Length; i++) {
                Assert.AreEqual(types[i], palette.Read(i), "Invalid registered type");
            }
        }
    }
}