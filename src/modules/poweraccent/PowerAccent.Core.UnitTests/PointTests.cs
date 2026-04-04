// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerAccent.Core.UnitTests
{
    [TestClass]
    public class PointTests
    {
        [TestMethod]
        public void DefaultConstructor_ShouldInitializeToZero()
        {
            var point = new Point();
            Assert.AreEqual(0, point.X);
            Assert.AreEqual(0, point.Y);
        }

        [TestMethod]
        public void DoubleConstructor_ShouldSetCoordinates()
        {
            var point = new Point(3.5, 7.2);
            Assert.AreEqual(3.5, point.X);
            Assert.AreEqual(7.2, point.Y);
        }

        [TestMethod]
        public void IntConstructor_ShouldSetCoordinates()
        {
            var point = new Point(10, 20);
            Assert.AreEqual(10.0, point.X);
            Assert.AreEqual(20.0, point.Y);
        }

        [TestMethod]
        public void DrawingPointConstructor_ShouldConvertCoordinates()
        {
            var drawingPoint = new System.Drawing.Point(15, 25);
            var point = new Point(drawingPoint);
            Assert.AreEqual(15.0, point.X);
            Assert.AreEqual(25.0, point.Y);
        }

        [TestMethod]
        public void ImplicitConversion_FromDrawingPoint_ShouldWork()
        {
            Point point = new System.Drawing.Point(100, 200);
            Assert.AreEqual(100.0, point.X);
            Assert.AreEqual(200.0, point.Y);
        }

        [TestMethod]
        public void DivisionByScalar_ShouldDivideCoordinates()
        {
            var point = new Point(10.0, 20.0);
            var result = point / 2.0;
            Assert.AreEqual(5.0, result.X);
            Assert.AreEqual(10.0, result.Y);
        }

        [TestMethod]
        public void DivisionByScalar_WithNegativeDivider_ShouldNegateCoordinates()
        {
            var point = new Point(10.0, 20.0);
            var result = point / -2.0;
            Assert.AreEqual(-5.0, result.X);
            Assert.AreEqual(-10.0, result.Y);
        }

        [TestMethod]
        [ExpectedException(typeof(DivideByZeroException))]
        public void DivisionByScalar_WithZero_ShouldThrow()
        {
            var point = new Point(10.0, 20.0);
            _ = point / 0.0;
        }

        [TestMethod]
        public void DivisionByPoint_ShouldDivideComponentwise()
        {
            var point = new Point(10.0, 20.0);
            var divider = new Point(2.0, 5.0);
            var result = point / divider;
            Assert.AreEqual(5.0, result.X);
            Assert.AreEqual(4.0, result.Y);
        }

        [TestMethod]
        [ExpectedException(typeof(DivideByZeroException))]
        public void DivisionByPoint_WithZeroX_ShouldThrow()
        {
            var point = new Point(10.0, 20.0);
            var divider = new Point(0.0, 5.0);
            _ = point / divider;
        }

        [TestMethod]
        [ExpectedException(typeof(DivideByZeroException))]
        public void DivisionByPoint_WithZeroY_ShouldThrow()
        {
            var point = new Point(10.0, 20.0);
            var divider = new Point(5.0, 0.0);
            _ = point / divider;
        }

        [TestMethod]
        public void NegativeCoordinates_ShouldBeAllowed()
        {
            var point = new Point(-5.5, -10.3);
            Assert.AreEqual(-5.5, point.X);
            Assert.AreEqual(-10.3, point.Y);
        }

        [TestMethod]
        public void DivisionByScalar_WithFractionalDivider_ShouldWork()
        {
            var point = new Point(10.0, 20.0);
            var result = point / 0.5;
            Assert.AreEqual(20.0, result.X);
            Assert.AreEqual(40.0, result.Y);
        }
    }
}
