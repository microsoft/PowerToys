// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerAccent.Core.UnitTests
{
    [TestClass]
    public class PointTests
    {
        /// <summary>
        /// Product code: Point() default constructor
        /// What: Verifies X and Y initialize to zero
        /// Why: Default state must be deterministic — uninitialized coordinates cause misplacement
        /// </summary>
        [TestMethod]
        public void DefaultConstructor_ShouldInitializeToZero()
        {
            var point = new Point();
            Assert.AreEqual(0, point.X);
            Assert.AreEqual(0, point.Y);
        }

        /// <summary>
        /// Product code: Point(double, double) constructor
        /// What: Verifies that fractional coordinates are preserved exactly
        /// Why: Sub-pixel precision is needed for DPI-aware positioning
        /// </summary>
        [TestMethod]
        public void DoubleConstructor_ShouldSetCoordinates()
        {
            var point = new Point(3.5, 7.2);
            Assert.AreEqual(3.5, point.X);
            Assert.AreEqual(7.2, point.Y);
        }

        /// <summary>
        /// Product code: Point(int, int) constructor
        /// What: Verifies integer coordinates are stored as doubles without loss
        /// Why: Implicit int→double conversion must be exact for pixel-aligned positioning
        /// </summary>
        [TestMethod]
        public void IntConstructor_ShouldSetCoordinates()
        {
            var point = new Point(10, 20);
            Assert.AreEqual(10.0, point.X);
            Assert.AreEqual(20.0, point.Y);
        }

        /// <summary>
        /// Product code: Point(System.Drawing.Point) constructor
        /// What: Verifies conversion from System.Drawing.Point preserves coordinates
        /// Why: Win32 API interop uses System.Drawing.Point — conversion must be lossless
        /// </summary>
        [TestMethod]
        public void DrawingPointConstructor_ShouldConvertCoordinates()
        {
            var drawingPoint = new System.Drawing.Point(15, 25);
            var point = new Point(drawingPoint);
            Assert.AreEqual(15.0, point.X);
            Assert.AreEqual(25.0, point.Y);
        }

        /// <summary>
        /// Product code: Point implicit operator from System.Drawing.Point
        /// What: Verifies implicit conversion works at assignment sites
        /// Why: Enables seamless interop without explicit casts in calling code
        /// </summary>
        [TestMethod]
        public void ImplicitConversion_FromDrawingPoint_ShouldWork()
        {
            Point point = new System.Drawing.Point(100, 200);
            Assert.AreEqual(100.0, point.X);
            Assert.AreEqual(200.0, point.Y);
        }

        /// <summary>
        /// Product code: Point operator /(Point, double)
        /// What: Verifies scalar division halves both coordinates
        /// Why: Core arithmetic for DPI normalization
        /// </summary>
        [TestMethod]
        public void DivisionByScalar_ShouldDivideCoordinates()
        {
            var point = new Point(10.0, 20.0);
            var result = point / 2.0;
            Assert.AreEqual(5.0, result.X);
            Assert.AreEqual(10.0, result.Y);
        }

        /// <summary>
        /// Product code: Point operator /(Point, double) with negative divisor
        /// What: Verifies negative divisor negates both coordinates
        /// Why: Ensures correct sign propagation in coordinate math
        /// </summary>
        [TestMethod]
        public void DivisionByScalar_WithNegativeDivider_ShouldNegateCoordinates()
        {
            var point = new Point(10.0, 20.0);
            var result = point / -2.0;
            Assert.AreEqual(-5.0, result.X);
            Assert.AreEqual(-10.0, result.Y);
        }

        /// <summary>
        /// Product code: Point operator /(Point, double) zero guard
        /// What: Verifies that dividing by zero throws DivideByZeroException
        /// Why: Prevents Infinity coordinates from corrupting toolbar placement
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(DivideByZeroException))]
        public void DivisionByScalar_WithZero_ShouldThrow()
        {
            var point = new Point(10.0, 20.0);
            _ = point / 0.0;
        }

        /// <summary>
        /// Product code: Point operator /(Point, Point)
        /// What: Verifies component-wise division (X/X, Y/Y)
        /// Why: Used for coordinate space transformations
        /// </summary>
        [TestMethod]
        public void DivisionByPoint_ShouldDivideComponentwise()
        {
            var point = new Point(10.0, 20.0);
            var divider = new Point(2.0, 5.0);
            var result = point / divider;
            Assert.AreEqual(5.0, result.X);
            Assert.AreEqual(4.0, result.Y);
        }

        /// <summary>
        /// Product code: Point operator /(Point, Point) zero guard for X
        /// What: Verifies that a divider with X=0 throws DivideByZeroException
        /// Why: X=0 in a divider would produce Infinity for the X coordinate
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(DivideByZeroException))]
        public void DivisionByPoint_WithZeroX_ShouldThrow()
        {
            var point = new Point(10.0, 20.0);
            var divider = new Point(0.0, 5.0);
            _ = point / divider;
        }

        /// <summary>
        /// Product code: Point operator /(Point, Point) zero guard for Y
        /// What: Verifies that a divider with Y=0 throws DivideByZeroException
        /// Why: Y=0 in a divider would produce Infinity for the Y coordinate
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(DivideByZeroException))]
        public void DivisionByPoint_WithZeroY_ShouldThrow()
        {
            var point = new Point(10.0, 20.0);
            var divider = new Point(5.0, 0.0);
            _ = point / divider;
        }

        /// <summary>
        /// Product code: Point(double, double) constructor with negatives
        /// What: Verifies that negative coordinates are stored correctly
        /// Why: Negative coordinates are valid in multi-monitor setups (left/above primary)
        /// </summary>
        [TestMethod]
        public void NegativeCoordinates_ShouldBeAllowed()
        {
            var point = new Point(-5.5, -10.3);
            Assert.AreEqual(-5.5, point.X);
            Assert.AreEqual(-10.3, point.Y);
        }

        /// <summary>
        /// Product code: Point operator /(Point, double) with fractional divisor
        /// What: Verifies dividing by 0.5 effectively doubles coordinates
        /// Why: Fractional DPI values (1.25x, 1.5x) are common in production
        /// </summary>
        [TestMethod]
        public void DivisionByScalar_WithFractionalDivider_ShouldWork()
        {
            var point = new Point(10.0, 20.0);
            var result = point / 0.5;
            Assert.AreEqual(20.0, result.X);
            Assert.AreEqual(40.0, result.Y);
        }

        /// <summary>
        /// Product code: Point operator /(Point, Point) zero guard for both X and Y
        /// What: Verifies that a divider with both X=0 and Y=0 throws
        /// Why: Degenerate origin-point divider must be rejected — tests the first guard hit
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(DivideByZeroException))]
        public void DivisionByPoint_WithBothZero_ShouldThrow()
        {
            var point = new Point(10.0, 20.0);
            var divider = new Point(0.0, 0.0);
            _ = point / divider;
        }
    }
}
