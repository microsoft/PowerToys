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
        [TestMethod]
        public void DefaultConstructor_ShouldInitializeToZero()
        {
            var size = new Size();
            Assert.AreEqual(0, size.Width);
            Assert.AreEqual(0, size.Height);
        }

        [TestMethod]
        public void DoubleConstructor_ShouldSetDimensions()
        {
            var size = new Size(100.5, 200.3);
            Assert.AreEqual(100.5, size.Width);
            Assert.AreEqual(200.3, size.Height);
        }

        [TestMethod]
        public void IntConstructor_ShouldSetDimensions()
        {
            var size = new Size(800, 600);
            Assert.AreEqual(800.0, size.Width);
            Assert.AreEqual(600.0, size.Height);
        }

        [TestMethod]
        public void ImplicitConversion_FromDrawingSize_ShouldWork()
        {
            Size size = new System.Drawing.Size(1920, 1080);
            Assert.AreEqual(1920.0, size.Width);
            Assert.AreEqual(1080.0, size.Height);
        }

        [TestMethod]
        public void DivisionByScalar_ShouldDivideDimensions()
        {
            var size = new Size(100.0, 200.0);
            var result = size / 2.0;
            Assert.AreEqual(50.0, result.Width);
            Assert.AreEqual(100.0, result.Height);
        }

        [TestMethod]
        public void DivisionByScalar_WithFractionalDivider_ShouldWork()
        {
            var size = new Size(100.0, 200.0);
            var result = size / 0.5;
            Assert.AreEqual(200.0, result.Width);
            Assert.AreEqual(400.0, result.Height);
        }

        [TestMethod]
        [ExpectedException(typeof(DivideByZeroException))]
        public void DivisionByScalar_WithZero_ShouldThrow()
        {
            var size = new Size(100.0, 200.0);
            _ = size / 0.0;
        }

        [TestMethod]
        public void DivisionBySize_ShouldDivideComponentwise()
        {
            var size = new Size(100.0, 200.0);
            var divider = new Size(10.0, 20.0);
            var result = size / divider;
            Assert.AreEqual(10.0, result.Width);
            Assert.AreEqual(10.0, result.Height);
        }

        [TestMethod]
        [ExpectedException(typeof(DivideByZeroException))]
        public void DivisionBySize_WithZeroWidth_ShouldThrow()
        {
            var size = new Size(100.0, 200.0);
            var divider = new Size(0.0, 20.0);
            _ = size / divider;
        }

        [TestMethod]
        [ExpectedException(typeof(DivideByZeroException))]
        public void DivisionBySize_WithZeroHeight_ShouldThrow()
        {
            var size = new Size(100.0, 200.0);
            var divider = new Size(10.0, 0.0);
            _ = size / divider;
        }

        [TestMethod]
        public void DivisionByScalar_WithNegativeDivider_ShouldWork()
        {
            var size = new Size(100.0, 200.0);
            var result = size / -2.0;
            Assert.AreEqual(-50.0, result.Width);
            Assert.AreEqual(-100.0, result.Height);
        }

        [TestMethod]
        public void DivisionByScalar_WithVerySmallDivider_ShouldYieldLargeResult()
        {
            var size = new Size(1.0, 1.0);
            var result = size / 0.001;
            Assert.AreEqual(1000.0, result.Width, 0.01);
            Assert.AreEqual(1000.0, result.Height, 0.01);
        }
    }
}
