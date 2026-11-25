// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Imaging;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using MouseJump.Common.Bezels;
using MouseJump.Models.Styles;

namespace MouseJump.Common.UnitTests.Bezels;

public static class BezelRendererTests
{
    [TestClass]
    public sealed class ClampDepthTests
    {
        public sealed class TestCase
        {
            public TestCase(string testName, BorderStyle borderStyle, BorderStyle expectedResult)
            {
                this.TestName = testName;
                this.BorderStyle = borderStyle;
                this.ExpectedResult = expectedResult;
            }

            public string TestName { get; }

            public BorderStyle BorderStyle { get; }

            public BorderStyle ExpectedResult { get; }

            public override string ToString() => this.TestName;
        }

        public static IEnumerable<object[]> GetTestCases()
        {
            // depth well within half the thickness - unchanged
            yield return new object[]
            {
                new TestCase(
                    testName: "depth < thickness / 2 is unchanged",
                    borderStyle: new(Color.Red, all: 10, depth: 3),
                    expectedResult: new(Color.Red, all: 10, depth: 3)),
            };

            // depth exactly half the thickness - unchanged (still leaves a zero-width, not
            // negative-width, flat band in the middle)
            yield return new object[]
            {
                new TestCase(
                    testName: "depth == thickness / 2 is unchanged",
                    borderStyle: new(Color.Red, all: 10, depth: 5),
                    expectedResult: new(Color.Red, all: 10, depth: 5)),
            };

            // the example from the bug report: thickness 10, depth 8 renders as thickness 10,
            // depth 5 - the *stored* value (asserted separately in RunTestCases) stays 8
            yield return new object[]
            {
                new TestCase(
                    testName: "depth > thickness / 2 clamps to thickness / 2",
                    borderStyle: new(Color.Red, all: 10, depth: 8),
                    expectedResult: new(Color.Red, all: 10, depth: 5)),
            };

            // the actual reported repro values - thickness 25, depth 23
            yield return new object[]
            {
                new TestCase(
                    testName: "reported repro - thickness 25, depth 23 clamps to 12.5",
                    borderStyle: new(Color.Red, all: 25, depth: 23),
                    expectedResult: new(Color.Red, all: 25, depth: 12.5m)),
            };

            // asymmetric thickness - clamp is driven by the *thinnest* side, so corners on that
            // side can't overlap either
            yield return new object[]
            {
                new TestCase(
                    testName: "asymmetric thickness clamps to the thinnest side / 2",
                    borderStyle: new(Color.Red, left: 10, top: 20, right: 10, bottom: 20, depth: 8),
                    expectedResult: new(Color.Red, left: 10, top: 20, right: 10, bottom: 20, depth: 5)),
            };

            // zero thickness - any positive depth clamps down to zero
            yield return new object[]
            {
                new TestCase(
                    testName: "zero thickness clamps depth to zero",
                    borderStyle: new(Color.Red, all: 0, depth: 4),
                    expectedResult: new(Color.Red, all: 0, depth: 0)),
            };

            // depth already zero - unchanged
            yield return new object[]
            {
                new TestCase(
                    testName: "zero depth is unchanged",
                    borderStyle: new(Color.Red, all: 10, depth: 0),
                    expectedResult: new(Color.Red, all: 10, depth: 0)),
            };
        }

        [TestMethod]
        [DynamicData(nameof(GetTestCases))]
        public void RunTestCases(TestCase data)
        {
            var actual = BezelRenderer.ClampDepth(data.BorderStyle);
            var expected = data.ExpectedResult;

            Assert.AreEqual(expected.Color, actual.Color);
            Assert.AreEqual(expected.Left, actual.Left);
            Assert.AreEqual(expected.Top, actual.Top);
            Assert.AreEqual(expected.Right, actual.Right);
            Assert.AreEqual(expected.Bottom, actual.Bottom);
            Assert.AreEqual(expected.Depth, actual.Depth);

            // ClampDepth must never mutate the caller's own BorderStyle (it can't - BorderStyle
            // has no setters - but it must also not just hand the same instance back once it's
            // decided to clamp) - config/settings should keep whatever Depth the user actually
            // entered, even when rendering clamps it.
            if (data.BorderStyle.Depth != expected.Depth)
            {
                Assert.AreNotSame(data.BorderStyle, actual);
            }
        }
    }

    [TestClass]
    public sealed class DrawBezelTests
    {
        // thickness=12, depth=4 -> outer highlight ring [0,4), flat zone [4,8),
        // inner shadow ring [8,12) on each edge
        private const int Thickness = 12;
        private const int Depth = 4;
        private const int ImageSize = 60;

        [TestMethod]
        public void DrawBezel_LeavesInteriorUntouched()
        {
            using var bitmap = DrawBezelTests.RenderBezel(out _);

            // the content area inside the ring is never drawn to - DrawBezel draws a frame,
            // not a filled rectangle - so it must still be the fully-transparent pixel the
            // bitmap started with
            var interior = bitmap.GetPixel(ImageSize / 2, ImageSize / 2);
            Assert.AreEqual(Color.FromArgb(0, 0, 0, 0), interior);
        }

        [TestMethod]
        public void DrawBezel_FlatZoneIsUnlitBorderColor()
        {
            using var bitmap = DrawBezelTests.RenderBezel(out var borderColor);

            // position 6 sits inside [depth, thickness-depth) = [4, 8), the flat zone with no
            // lighting effect - well clear of both corners (x is mid-edge), so this pixel
            // should be exactly the plain, unlit border colour
            var flatZonePixel = bitmap.GetPixel(ImageSize / 2, 6);
            Assert.AreEqual(borderColor, flatZonePixel);
        }

        [TestMethod]
        public void DrawBezel_OuterTopEdgeIsHighlighted()
        {
            using var bitmap = DrawBezelTests.RenderBezel(out var borderColor);

            // the outermost row of the top edge, clear of both corners - light source is
            // top-left (see DrawBezel's own doc), so this should be lightened, not darkened
            var highlightPixel = bitmap.GetPixel(20, 0);
            Assert.IsTrue(
                highlightPixel.R > borderColor.R && highlightPixel.G > borderColor.G && highlightPixel.B > borderColor.B,
                $"expected a pixel lighter than {borderColor}, got {highlightPixel}");
        }

        [TestMethod]
        public void DrawBezel_OuterBottomEdgeNearCornerIsShadowed()
        {
            using var bitmap = DrawBezelTests.RenderBezel(out var borderColor);

            // the outermost row of the bottom edge, close to the bottom-right corner (the
            // strong/corner-adjacent end of that edge's gradient) - facing away from the
            // top-left light source, so this should be darkened, not lightened
            var shadowPixel = bitmap.GetPixel(40, ImageSize - 1);
            Assert.IsTrue(
                shadowPixel.R < borderColor.R && shadowPixel.G < borderColor.G && shadowPixel.B < borderColor.B,
                $"expected a pixel darker than {borderColor}, got {shadowPixel}");
        }

        private static Bitmap RenderBezel(out Color borderColor)
        {
            borderColor = Color.FromArgb(255, 100, 100, 100); // mid-grey - room to lighten or darken either way
            var borderStyle = new BorderStyle(borderColor, all: Thickness, depth: Depth);
            using var renderer = new BezelRenderer(borderStyle);

            var bitmap = new Bitmap(ImageSize, ImageSize, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bitmap);
            g.Clear(Color.Transparent);
            renderer.DrawBezel(g, 0, 0, ImageSize, ImageSize);
            return bitmap;
        }
    }
}
