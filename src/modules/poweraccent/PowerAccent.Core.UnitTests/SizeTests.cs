// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerAccent.Core.UnitTests
{
    [TestClass]
    public class SizeTests
    {
        /// <summary>
        /// Product code: Size() default constructor
        /// What: Verifies Width and Height initialize to zero
        /// Why: Default state must be deterministic to avoid layout corruption
        /// </summary>
        [TestMethod]
        public void DefaultConstructor_ShouldInitializeToZero()
        {
            var size = new Size();
            Assert.AreEqual(0, size.Width);
            Assert.AreEqual(0, size.Height);
        }

        /// <summary>
        /// Product code: Size(double, double) constructor
        /// What: Verifies fractional values are preserved exactly
        /// Why: Ensures no rounding during construction for sub-pixel precision
        /// </summary>
        [TestMethod]
        public void DoubleConstructor_ShouldSetDimensions()
        {
            var size = new Size(100.5, 200.3);
            Assert.AreEqual(100.5, size.Width);
            Assert.AreEqual(200.3, size.Height);
        }

        /// <summary>
        /// Product code: Size(int, int) constructor
        /// What: Verifies integer dimensions are stored as doubles without loss
        /// Why: Implicit int→double conversion must be exact
        /// </summary>
        [TestMethod]
        public void IntConstructor_ShouldSetDimensions()
        {
            var size = new Size(800, 600);
            Assert.AreEqual(800.0, size.Width);
            Assert.AreEqual(600.0, size.Height);
        }

        /// <summary>
        /// Product code: Size implicit operator from System.Drawing.Size
        /// What: Verifies implicit conversion from System.Drawing.Size works
        /// Why: Interop boundary — System.Drawing types are used by Win32 APIs
        /// </summary>
        [TestMethod]
        public void ImplicitConversion_FromDrawingSize_ShouldWork()
        {
            Size size = new System.Drawing.Size(1920, 1080);
            Assert.AreEqual(1920.0, size.Width);
            Assert.AreEqual(1080.0, size.Height);
        }

        /// <summary>
        /// Product code: Size operator /(Size, double)
        /// What: Verifies scalar division halves both dimensions
        /// Why: Core arithmetic used for DPI scaling — must produce exact results
        /// </summary>
        [TestMethod]
        public void DivisionByScalar_ShouldDivideDimensions()
        {
            var size = new Size(100.0, 200.0);
            var result = size / 2.0;
            Assert.AreEqual(50.0, result.Width);
            Assert.AreEqual(100.0, result.Height);
        }

        /// <summary>
        /// Product code: Size operator /(Size, double) with fractional divisor
        /// What: Verifies that dividing by 0.5 effectively doubles dimensions
        /// Why: Fractional DPI values are real (e.g., 1.25x, 1.5x scaling)
        /// </summary>
        [TestMethod]
        public void DivisionByScalar_WithFractionalDivider_ShouldWork()
        {
            var size = new Size(100.0, 200.0);
            var result = size / 0.5;
            Assert.AreEqual(200.0, result.Width);
            Assert.AreEqual(400.0, result.Height);
        }

        /// <summary>
        /// Product code: Size operator /(Size, double) zero guard
        /// What: Verifies that dividing by zero throws DivideByZeroException
        /// Why: Prevents Infinity dimensions that would corrupt window layout
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(DivideByZeroException))]
        public void DivisionByScalar_WithZero_ShouldThrow()
        {
            var size = new Size(100.0, 200.0);
            _ = size / 0.0;
        }

        /// <summary>
        /// Product code: Size operator /(Size, Size)
        /// What: Verifies component-wise division (Width/Width, Height/Height)
        /// Why: Used for computing scaling ratios between two rectangles
        /// </summary>
        [TestMethod]
        public void DivisionBySize_ShouldDivideComponentwise()
        {
            var size = new Size(100.0, 200.0);
            var divider = new Size(10.0, 20.0);
            var result = size / divider;
            Assert.AreEqual(10.0, result.Width);
            Assert.AreEqual(10.0, result.Height);
        }

        /// <summary>
        /// Product code: Size operator /(Size, Size) zero Width guard
        /// What: Verifies that a divider with Width=0 throws DivideByZeroException
        /// Why: Width=0 divider would produce Infinity — must be caught early
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(DivideByZeroException))]
        public void DivisionBySize_WithZeroWidth_ShouldThrow()
        {
            var size = new Size(100.0, 200.0);
            var divider = new Size(0.0, 20.0);
            _ = size / divider;
        }

        /// <summary>
        /// Product code: Size operator /(Size, Size) zero Height guard
        /// What: Verifies that a divider with Height=0 throws DivideByZeroException
        /// Why: Height=0 divider would produce Infinity — must be caught early
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(DivideByZeroException))]
        public void DivisionBySize_WithZeroHeight_ShouldThrow()
        {
            var size = new Size(100.0, 200.0);
            var divider = new Size(10.0, 0.0);
            _ = size / divider;
        }

        /// <summary>
        /// Product code: Size operator /(Size, double) with negative divisor
        /// What: Verifies that negative divisor correctly negates both dimensions
        /// Why: Ensures sign propagation is correct in arithmetic
        /// </summary>
        [TestMethod]
        public void DivisionByScalar_WithNegativeDivider_ShouldWork()
        {
            var size = new Size(100.0, 200.0);
            var result = size / -2.0;
            Assert.AreEqual(-50.0, result.Width);
            Assert.AreEqual(-100.0, result.Height);
        }

        /// <summary>
        /// Product code: Size operator /(Size, double) with very small divisor
        /// What: Verifies division by a small number produces expected large result
        /// Why: Near-zero divisors (e.g., very low DPI) must not crash or produce NaN
        /// </summary>
        [TestMethod]
        public void DivisionByScalar_WithVerySmallDivider_ShouldYieldLargeResult()
        {
            var size = new Size(1.0, 1.0);
            var result = size / 0.001;
            Assert.AreEqual(1000.0, result.Width, 0.01);
            Assert.AreEqual(1000.0, result.Height, 0.01);
        }

        /// <summary>
        /// Product code: Size operator /(Size, Size) zero guard for both dimensions
        /// What: Verifies that a divider with both Width=0 and Height=0 throws
        /// Why: Degenerate zero-size divider must be rejected — tests the first guard hit
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(DivideByZeroException))]
        public void DivisionBySize_WithBothZeroDimensions_ShouldThrow()
        {
            var size = new Size(100.0, 200.0);
            var divider = new Size(0.0, 0.0);
            _ = size / divider;
        }
    }
}
