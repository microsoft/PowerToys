// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseJump.Models.Drawing;
using MouseJump.Models.Styles;

namespace MouseJump.Models.UnitTests.Drawing;

public static class BoxBoundsTests
{
    [TestClass]
    public sealed class CreateFromContentBounds
    {
        public sealed class TestCase
        {
            public TestCase(RectangleInfo contentBounds, BoxStyle boxStyle, BoxBounds expectedResult)
            {
                this.ContentBounds = contentBounds;
                this.BoxStyle = boxStyle;
                this.ExpectedResult = expectedResult;
            }

            public RectangleInfo ContentBounds { get; }

            public BoxStyle BoxStyle { get; }

            public BoxBounds ExpectedResult { get; }
        }

        public static IEnumerable<object[]> GetTestCases()
        {
            yield return new object[]
            {
                new TestCase(
                    contentBounds: new(100, 100, 800, 600),
                    boxStyle: new(
                        marginStyle: new(3),
                        borderStyle: new(Color.Red, 5, 0),
                        paddingStyle: new(7),
                        backgroundStyle: BackgroundStyle.Empty),
                    expectedResult: new(
                        outerBounds: new(85, 85, 830, 630),
                        marginBounds: new(85, 85, 830, 630),
                        borderBounds: new(88, 88, 824, 624),
                        paddingBounds: new(93, 93, 814, 614),
                        contentBounds: new(100, 100, 800, 600))),
            };
        }

        [TestMethod]
        [DynamicData(nameof(GetTestCases))]
        public void RunTestCases(TestCase data)
        {
            var actual = BoxBounds.CreateFromContentBounds(data.ContentBounds, data.BoxStyle);
            var expected = data.ExpectedResult;
            Assert.AreEqual(
            JsonSerializer.Serialize(expected),
            JsonSerializer.Serialize(actual));
        }
    }

    [TestClass]
    public sealed class CreateFromOuterBounds
    {
        public sealed class TestCase
        {
            public TestCase(RectangleInfo outerBounds, BoxStyle boxStyle, BoxBounds expectedResult)
            {
                this.OuterBounds = outerBounds;
                this.BoxStyle = boxStyle;
                this.ExpectedResult = expectedResult;
            }

            public RectangleInfo OuterBounds { get; }

            public BoxStyle BoxStyle { get; }

            public BoxBounds ExpectedResult { get; }
        }

        public static IEnumerable<object[]> GetTestCases()
        {
            yield return new object[]
            {
                new TestCase(
                    outerBounds: new(85, 85, 830, 630),
                    boxStyle: new(
                        marginStyle: new(3),
                        borderStyle: new(Color.Red, 5, 0),
                        paddingStyle: new(7),
                        backgroundStyle: BackgroundStyle.Empty),
                    expectedResult: new(
                        outerBounds: new(85, 85, 830, 630),
                        marginBounds: new(85, 85, 830, 630),
                        borderBounds: new(88, 88, 824, 624),
                        paddingBounds: new(93, 93, 814, 614),
                        contentBounds: new(100, 100, 800, 600))),
            };
        }

        [TestMethod]
        [DynamicData(nameof(GetTestCases))]
        public void RunTestCases(TestCase data)
        {
            var actual = BoxBounds.CreateFromOuterBounds(data.OuterBounds, data.BoxStyle);
            var expected = data.ExpectedResult;
            Assert.AreEqual(
                JsonSerializer.Serialize(expected),
                JsonSerializer.Serialize(actual));
        }
    }
}
