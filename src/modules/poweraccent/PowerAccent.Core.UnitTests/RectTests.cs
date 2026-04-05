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
        [TestMethod]
        public void DefaultConstructor_ShouldInitializeToZero()
        {
            var rect = new Rect();
            Assert.AreEqual(0, rect.X);
            Assert.AreEqual(0, rect.Y);
            Assert.AreEqual(0, rect.Width);
            Assert.AreEqual(0, rect.Height);
        }

        [TestMethod]
        public void IntConstructor_ShouldSetAllProperties()
        {
            var rect = new Rect(10, 20, 800, 600);
            Assert.AreEqual(10.0, rect.X);
            Assert.AreEqual(20.0, rect.Y);
            Assert.AreEqual(800.0, rect.Width);
            Assert.AreEqual(600.0, rect.Height);
        }

        [TestMethod]
        public void DoubleConstructor_ShouldSetAllProperties()
        {
            var rect = new Rect(1.5, 2.5, 100.3, 200.7);
            Assert.AreEqual(1.5, rect.X);
            Assert.AreEqual(2.5, rect.Y);
            Assert.AreEqual(100.3, rect.Width);
            Assert.AreEqual(200.7, rect.Height);
        }

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

        [TestMethod]
        [ExpectedException(typeof(DivideByZeroException))]
        public void DivisionByScalar_WithZero_ShouldThrow()
        {
            var rect = new Rect(10.0, 20.0, 100.0, 200.0);
            _ = rect / 0.0;
        }

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

        [TestMethod]
        [ExpectedException(typeof(DivideByZeroException))]
        public void DivisionByRect_WithZeroX_ShouldThrow()
        {
            var rect = new Rect(10.0, 20.0, 100.0, 200.0);
            var divider = new Rect(0.0, 5.0, 10.0, 20.0);
            _ = rect / divider;
        }

        [TestMethod]
        [ExpectedException(typeof(DivideByZeroException))]
        public void DivisionByRect_WithZeroY_ShouldThrow()
        {
            var rect = new Rect(10.0, 20.0, 100.0, 200.0);
            var divider = new Rect(5.0, 0.0, 10.0, 20.0);
            _ = rect / divider;
        }

        [TestMethod]
        public void NegativeCoordinates_ShouldBeAllowed()
        {
            var rect = new Rect(-100.0, -200.0, 400.0, 300.0);
            Assert.AreEqual(-100.0, rect.X);
            Assert.AreEqual(-200.0, rect.Y);
            Assert.AreEqual(400.0, rect.Width);
            Assert.AreEqual(300.0, rect.Height);
        }

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
