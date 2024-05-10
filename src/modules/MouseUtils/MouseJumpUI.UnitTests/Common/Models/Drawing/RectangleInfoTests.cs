// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseJumpUI.Common.Models.Drawing;

namespace MouseJumpUI.UnitTests.Common.Models.Drawing;

[TestClass]
public static class RectangleInfoTests
{
    [TestClass]
    public sealed class CenterTests
    {
        public sealed class TestCase
        {
            public TestCase(RectangleInfo rectangle, PointInfo point, RectangleInfo expectedResult)
            {
                this.Rectangle = rectangle;
                this.Point = point;
                this.ExpectedResult = expectedResult;
            }

            public RectangleInfo Rectangle { get; }

            public PointInfo Point { get; }

            public RectangleInfo ExpectedResult { get; }
        }

        public static IEnumerable<object[]> GetTestCases()
        {
            // zero-sized
            yield return new object[] { new TestCase(new(0, 0, 0, 0), new(0, 0), new(0, 0, 0, 0)), };

            // zero-origin
            yield return new object[] { new TestCase(new(0, 0, 200, 200), new(100, 100), new(0, 0, 200, 200)), };
            yield return new object[] { new TestCase(new(0, 0, 200, 200), new(500, 500), new(400, 400, 200, 200)), };
            yield return new object[] { new TestCase(new(0, 0, 800, 600), new(1000, 1000), new(600, 700, 800, 600)), };

            // non-zero origin
            yield return new object[] { new TestCase(new(1000, 2000, 200, 200), new(100, 100), new(0, 0, 200, 200)), };
            yield return new object[] { new TestCase(new(1000, 2000, 200, 200), new(500, 500), new(400, 400, 200, 200)), };
            yield return new object[] { new TestCase(new(1000, 2000, 800, 600), new(1000, 1000), new(600, 700, 800, 600)), };

            // negative result
            yield return new object[] { new TestCase(new(0, 0, 1000, 1200), new(300, 300), new(-200, -300, 1000, 1200)), };
        }

        [TestMethod]
        [DynamicData(nameof(GetTestCases), DynamicDataSourceType.Method)]
        public void RunTestCases(TestCase data)
        {
            var actual = data.Rectangle.Center(data.Point);
            var expected = data.ExpectedResult;
            Assert.AreEqual(expected.X, actual.X);
            Assert.AreEqual(expected.Y, actual.Y);
            Assert.AreEqual(expected.Width, actual.Width);
            Assert.AreEqual(expected.Height, actual.Height);
        }
    }

    [TestClass]
    public sealed class ClampTests
    {
        public sealed class TestCase
        {
            public TestCase(RectangleInfo inner, RectangleInfo outer, RectangleInfo expectedResult)
            {
                this.Inner = inner;
                this.Outer = outer;
                this.ExpectedResult = expectedResult;
            }

            public RectangleInfo Inner { get; }

            public RectangleInfo Outer { get; }

            public RectangleInfo ExpectedResult { get; }
        }

        public static IEnumerable<object[]> GetTestCases()
        {
            // already inside - obj fills bounds exactly
            yield return new object[]
            {
                new TestCase(new(0, 0, 100, 100), new(0, 0, 100, 100), new(0, 0, 100, 100)),
            };

            // already inside - obj exactly in each corner
            yield return new object[]
            {
                new TestCase(new(0, 0, 100, 100), new(0, 0, 200, 200), new(0, 0, 100, 100)),
            };
            yield return new object[]
            {
                new TestCase(new(100, 0, 100, 100), new(0, 0, 200, 200), new(100, 0, 100, 100)),
            };
            yield return new object[]
            {
                new TestCase(new(0, 100, 100, 100), new(0, 0, 200, 200), new(0, 100, 100, 100)),
            };
            yield return new object[]
            {
                new TestCase(new(100, 100, 100, 100), new(0, 0, 200, 200), new(100, 100, 100, 100)),
            };

            // move inside - obj outside each corner
            yield return new object[]
            {
                new TestCase(new(-50, -50, 100, 100), new(0, 0, 200, 200), new(0, 0, 100, 100)),
            };
            yield return new object[]
            {
                new TestCase(new(250, -50, 100, 100), new(0, 0, 200, 200), new(100, 0, 100, 100)),
            };
            yield return new object[]
            {
                new TestCase(new(-50, 250, 100, 100), new(0, 0, 200, 200), new(0, 100, 100, 100)),
            };
            yield return new object[]
            {
                new TestCase(new(150, 150, 100, 100), new(0, 0, 200, 200), new(100, 100, 100, 100)),
            };
        }

        [TestMethod]
        [DynamicData(nameof(GetTestCases), DynamicDataSourceType.Method)]
        public void RunTestCases(TestCase data)
        {
            var actual = data.Inner.Clamp(data.Outer);
            var expected = data.ExpectedResult;
            Assert.AreEqual(expected.X, actual.X);
            Assert.AreEqual(expected.Y, actual.Y);
            Assert.AreEqual(expected.Width, actual.Width);
            Assert.AreEqual(expected.Height, actual.Height);
        }
    }
}
