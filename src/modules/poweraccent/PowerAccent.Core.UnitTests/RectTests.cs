// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerAccent.Core.UnitTests
{
    [TestClass]
    public class RectTests
    {
        /// <summary>
        /// Product code: Rect() default constructor
        /// What: Verifies all properties initialize to zero
        /// Why: Default state must be well-defined to avoid uninitialized geometry bugs
        /// </summary>
        [TestMethod]
        public void DefaultConstructor_ShouldInitializeToZero()
        {
            var rect = new Rect();
            Assert.AreEqual(0, rect.X);
            Assert.AreEqual(0, rect.Y);
            Assert.AreEqual(0, rect.Width);
            Assert.AreEqual(0, rect.Height);
        }

        /// <summary>
        /// Product code: Rect(int, int, int, int) constructor
        /// What: Verifies that integer arguments are correctly stored as doubles
        /// Why: Implicit int→double conversion must preserve exact values
        /// </summary>
        [TestMethod]
        public void IntConstructor_ShouldSetAllProperties()
        {
            var rect = new Rect(10, 20, 800, 600);
            Assert.AreEqual(10.0, rect.X);
            Assert.AreEqual(20.0, rect.Y);
            Assert.AreEqual(800.0, rect.Width);
            Assert.AreEqual(600.0, rect.Height);
        }

        /// <summary>
        /// Product code: Rect(double, double, double, double) constructor
        /// What: Verifies that fractional double values are stored exactly
        /// Why: Ensures no rounding or truncation occurs during construction
        /// </summary>
        [TestMethod]
        public void DoubleConstructor_ShouldSetAllProperties()
        {
            var rect = new Rect(1.5, 2.5, 100.3, 200.7);
            Assert.AreEqual(1.5, rect.X);
            Assert.AreEqual(2.5, rect.Y);
            Assert.AreEqual(100.3, rect.Width);
            Assert.AreEqual(200.7, rect.Height);
        }

        /// <summary>
        /// Product code: Rect(Point, Size) constructor
        /// What: Verifies that Point and Size components map to correct Rect properties
        /// Why: X/Y come from Point, Width/Height from Size — a swap would silently corrupt layout
        /// </summary>
        [TestMethod]
        public void PointSizeConstructor_ShouldSetFromComponents()
        {
            var point = new Point(50.0, 100.0);
            var size = new Size(400.0, 300.0);
            var rect = new Rect(point, size);
            Assert.AreEqual(50.0, rect.X);
            Assert.AreEqual(100.0, rect.Y);
            Assert.AreEqual(400.0, rect.Width);
            Assert.AreEqual(300.0, rect.Height);
        }

        /// <summary>
        /// Product code: Rect operator /(Rect, double)
        /// What: Verifies component-wise division by a scalar
        /// Why: Used for DPI scaling — incorrect division would misposition the toolbar
        /// </summary>
        [TestMethod]
        public void DivisionByScalar_ShouldDivideAllComponents()
        {
            var rect = new Rect(10.0, 20.0, 100.0, 200.0);
            var result = rect / 2.0;
            Assert.AreEqual(5.0, result.X);
            Assert.AreEqual(10.0, result.Y);
            Assert.AreEqual(50.0, result.Width);
            Assert.AreEqual(100.0, result.Height);
        }

        /// <summary>
        /// Product code: Rect operator /(Rect, double) zero guard
        /// What: Verifies that dividing by zero throws DivideByZeroException
        /// Why: Prevents Infinity values from propagating through layout calculations
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(DivideByZeroException))]
        public void DivisionByScalar_WithZero_ShouldThrow()
        {
            var rect = new Rect(10.0, 20.0, 100.0, 200.0);
            _ = rect / 0.0;
        }

        /// <summary>
        /// Product code: Rect operator /(Rect, Rect)
        /// What: Verifies component-wise division (X/X, Y/Y, Width/Width, Height/Height)
        /// Why: Used for coordinate system transformations between screen and window space
        /// </summary>
        [TestMethod]
        public void DivisionByRect_ShouldDivideComponentwise()
        {
            var rect = new Rect(10.0, 20.0, 100.0, 200.0);
            var divider = new Rect(2.0, 5.0, 10.0, 20.0);
            var result = rect / divider;
            Assert.AreEqual(5.0, result.X);
            Assert.AreEqual(4.0, result.Y);
            Assert.AreEqual(10.0, result.Width);
            Assert.AreEqual(10.0, result.Height);
        }

        /// <summary>
        /// Product code: Rect operator /(Rect, Rect) zero guard for X component
        /// What: Verifies that a divider with X=0 throws DivideByZeroException
        /// Why: X=0 in a divider Rect would produce Infinity for the X coordinate
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(DivideByZeroException))]
        public void DivisionByRect_WithZeroX_ShouldThrow()
        {
            var rect = new Rect(10.0, 20.0, 100.0, 200.0);
            var divider = new Rect(0.0, 5.0, 10.0, 20.0);
            _ = rect / divider;
        }

        /// <summary>
        /// Product code: Rect operator /(Rect, Rect) zero guard for Y component
        /// What: Verifies that a divider with Y=0 throws DivideByZeroException
        /// Why: Y=0 in a divider Rect would produce Infinity for the Y coordinate
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(DivideByZeroException))]
        public void DivisionByRect_WithZeroY_ShouldThrow()
        {
            var rect = new Rect(10.0, 20.0, 100.0, 200.0);
            var divider = new Rect(5.0, 0.0, 10.0, 20.0);
            _ = rect / divider;
        }

        /// <summary>
        /// Product code: Rect operator /(Rect, Rect) zero guard for Width component
        /// What: Verifies that a divider with Width=0 throws DivideByZeroException
        /// Why: Guards against producing Infinity values that propagate through calculations
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(DivideByZeroException))]
        public void DivisionByRect_WithZeroWidth_ShouldThrow()
        {
            var rect = new Rect(10, 20, 100, 200);
            var divider = new Rect(1, 1, 0, 1);
            _ = rect / divider;
        }

        /// <summary>
        /// Product code: Rect operator /(Rect, Rect) zero guard for Height component
        /// What: Verifies that a divider with Height=0 throws DivideByZeroException
        /// Why: Guards against producing Infinity values that propagate through calculations
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(DivideByZeroException))]
        public void DivisionByRect_WithZeroHeight_ShouldThrow()
        {
            var rect = new Rect(10, 20, 100, 200);
            var divider = new Rect(1, 1, 1, 0);
            _ = rect / divider;
        }

        /// <summary>
        /// Product code: Rect operator /(Rect, Rect) zero guard for all components
        /// What: Verifies that a divider with all zero components throws DivideByZeroException
        /// Why: Degenerate all-zero divider must be caught — tests the first guard hit (X=0)
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(DivideByZeroException))]
        public void DivisionByRect_WithAllZeroComponents_ShouldThrow()
        {
            var rect = new Rect(10, 20, 100, 200);
            var divider = new Rect(0, 0, 0, 0);
            _ = rect / divider;
        }

        /// <summary>
        /// Product code: Rect(double, double, double, double) constructor
        /// What: Verifies that negative X/Y coordinates are allowed
        /// Why: Multi-monitor setups can have negative screen coordinates (left/above primary)
        /// </summary>
        [TestMethod]
        public void NegativeCoordinates_ShouldBeAllowed()
        {
            var rect = new Rect(-100.0, -200.0, 400.0, 300.0);
            Assert.AreEqual(-100.0, rect.X);
            Assert.AreEqual(-200.0, rect.Y);
            Assert.AreEqual(400.0, rect.Width);
            Assert.AreEqual(300.0, rect.Height);
        }

        /// <summary>
        /// Product code: Rect operator /(Rect, double) with negative divisor
        /// What: Verifies that negative divisor negates all components
        /// Why: Ensures sign propagation is correct — used in coordinate flipping scenarios
        /// </summary>
        [TestMethod]
        public void DivisionByScalar_WithNegativeDivider_ShouldNegateComponents()
        {
            var rect = new Rect(10.0, 20.0, 100.0, 200.0);
            var result = rect / -1.0;
            Assert.AreEqual(-10.0, result.X);
            Assert.AreEqual(-20.0, result.Y);
            Assert.AreEqual(-100.0, result.Width);
            Assert.AreEqual(-200.0, result.Height);
        }
    }
}
