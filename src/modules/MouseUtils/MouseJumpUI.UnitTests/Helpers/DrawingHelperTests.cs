// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseJumpUI.Drawing;
using MouseJumpUI.Drawing.Models;

namespace MouseJumpUI.UnitTests.Helpers;

[TestClass]
public static class DrawingHelperTests
{
    [TestClass]
    public class CalculateLayoutInfoTests
    {
        public class TestCase
        {
            public TestCase(LayoutConfig layoutConfig, LayoutInfo expectedResult)
            {
                this.LayoutConfig = layoutConfig;
                this.ExpectedResult = expectedResult;
            }

            public LayoutConfig LayoutConfig { get; set; }

            public LayoutInfo ExpectedResult { get; set; }
        }

        public static IEnumerable<object[]> GetTestCases()
        {
            // happy path - check the preview form is shown
            // at the correct size and position on a single screen
            var layoutConfig = new LayoutConfig(
                virtualScreen: new(0, 0, 5120, 1440),
                screenBounds: new List<Rectangle>
                {
                    new(0, 0, 5120, 1440),
                },
                activatedLocation: new(5120 / 2, 1440 / 2),
                activatedScreen: 0,
                maximumFormSize: new(1600, 1200),
                formPadding: new(5, 5, 5, 5),
                previewPadding: new(0, 0, 0, 0));
            var layoutInfo = new LayoutInfo(
                layoutConfig: layoutConfig,
                formBounds: new(1760, 491.40625M, 1600, 457.1875M),
                previewBounds: new(0, 0, 1590, 447.1875M),
                screenBounds: new List<RectangleInfo>
                {
                    new(0, 0, 1590, 447.1875M),
                },
                activatedScreen: new(0, 0, 5120, 1440));
            yield return new[] { new TestCase(layoutConfig, layoutInfo) };

            // check we handle rounding errors in scaling the preview form
            // that might make the form *larger* than the current screen -
            // e.g. a desktop 7168 x 1440 scaled to a screen 1024 x 768
            // with a 5px form padding border:
            //
            // ((decimal)1014 / 7168) * 7168 = 1014.0000000000000000000000002
            layoutConfig = new LayoutConfig(
                virtualScreen: new(0, 0, 7168, 1440),
                screenBounds: new List<Rectangle>
                {
                    new(6144, 0, 1024, 768),
                    new(0, 0, 6144, 1440),
                },
                activatedLocation: new(6656, 384),
                activatedScreen: 0,
                maximumFormSize: new(1600, 1200),
                formPadding: new(5, 5, 5, 5),
                previewPadding: new(0, 0, 0, 0));
            layoutInfo = new LayoutInfo(
                layoutConfig: layoutConfig,
                formBounds: new(6144, 277.14732M, 1024, 213.70535M),
                previewBounds: new(0, 0, 1014, 203.70535M),
                screenBounds: new List<RectangleInfo>
                {
                    new(869.14285M, 0, 144.85714M, 108.642857M),
                    new(0, 0, 869.142857M, 203.705357M),
                },
                activatedScreen: new(6144, 0, 1024, 768));
            yield return new[] { new TestCase(layoutConfig, layoutInfo) };

            // check we handle rounding errors in scaling the preview form
            // that might make the form a pixel *smaller* than the current screen -
            // e.g. a desktop 7168 x 1440 scaled to a screen 1024 x 768
            // with a 5px form padding border:
            //
            // ((decimal)1280 / 7424) * 7424 = 1279.9999999999999999999999999
            layoutConfig = new LayoutConfig(
                virtualScreen: new(0, 0, 7424, 1440),
                screenBounds: new List<Rectangle>
                {
                    new(6144, 0, 1280, 768),
                    new(0, 0, 6144, 1440),
                },
                activatedLocation: new(6784, 384),
                activatedScreen: 0,
                maximumFormSize: new(1600, 1200),
                formPadding: new(5, 5, 5, 5),
                previewPadding: new(0, 0, 0, 0));
            layoutInfo = new LayoutInfo(
                layoutConfig: layoutConfig,
                formBounds: new(
                    6144,
                    255.83189M, // (768 - (((decimal)(1280-10) / 7424 * 1440) + 10)) / 2
                    1280,
                    256.33620M // ((decimal)(1280 - 10) / 7424 * 1440) + 10
                ),
                previewBounds: new(0, 0, 1270, 246.33620M),
                screenBounds: new List<RectangleInfo>
                {
                    new(1051.03448M, 0, 218.96551M, 131.37931M),
                    new(0, 0M, 1051.03448M, 246.33620M),
                },
                activatedScreen: new(6144, 0, 1280, 768));
            yield return new[] { new TestCase(layoutConfig, layoutInfo) };
        }

        [TestMethod]
        [DynamicData(nameof(GetTestCases), DynamicDataSourceType.Method)]
        public void RunTestCases(TestCase data)
        {
            // note - even if values are within 0.0001M of each other they could
            // still round to different values - e.g.
            // (int)1279.999999999999 -> 1279
            // vs
            // (int)1280.000000000000 -> 1280
            // so we'll compare the raw values, *and* convert to an int-based
            // Rectangle to compare rounded values
            var actual = DrawingHelper.CalculateLayoutInfo(data.LayoutConfig);
            var expected = data.ExpectedResult;
            Assert.AreEqual(expected.FormBounds.X, actual.FormBounds.X, 0.00001M, "FormBounds.X");
            Assert.AreEqual(expected.FormBounds.Y, actual.FormBounds.Y, 0.00001M, "FormBounds.Y");
            Assert.AreEqual(expected.FormBounds.Width, actual.FormBounds.Width, 0.00001M, "FormBounds.Width");
            Assert.AreEqual(expected.FormBounds.Height, actual.FormBounds.Height, 0.00001M, "FormBounds.Height");
            Assert.AreEqual(expected.FormBounds.ToRectangle(), actual.FormBounds.ToRectangle(), "FormBounds.ToRectangle");
            Assert.AreEqual(expected.PreviewBounds.X, actual.PreviewBounds.X, 0.00001M, "PreviewBounds.X");
            Assert.AreEqual(expected.PreviewBounds.Y, actual.PreviewBounds.Y, 0.00001M, "PreviewBounds.Y");
            Assert.AreEqual(expected.PreviewBounds.Width, actual.PreviewBounds.Width, 0.00001M, "PreviewBounds.Width");
            Assert.AreEqual(expected.PreviewBounds.Height, actual.PreviewBounds.Height, 0.00001M, "PreviewBounds.Height");
            Assert.AreEqual(expected.PreviewBounds.ToRectangle(), actual.PreviewBounds.ToRectangle(), "PreviewBounds.ToRectangle");
            Assert.AreEqual(expected.ScreenBounds.Count, actual.ScreenBounds.Count, "ScreenBounds.Count");
            for (var i = 0; i < expected.ScreenBounds.Count; i++)
            {
                Assert.AreEqual(expected.ScreenBounds[i].X, actual.ScreenBounds[i].X, 0.00001M, $"ScreenBounds[{i}].X");
                Assert.AreEqual(expected.ScreenBounds[i].Y, actual.ScreenBounds[i].Y, 0.00001M, $"ScreenBounds[{i}].Y");
                Assert.AreEqual(expected.ScreenBounds[i].Width, actual.ScreenBounds[i].Width, 0.00001M, $"ScreenBounds[{i}].Width");
                Assert.AreEqual(expected.ScreenBounds[i].Height, actual.ScreenBounds[i].Height, 0.00001M, $"ScreenBounds[{i}].Height");
                Assert.AreEqual(expected.ScreenBounds[i].ToRectangle(), actual.ScreenBounds[i].ToRectangle(), "ActivatedScreen.ToRectangle");
            }

            Assert.AreEqual(expected.ActivatedScreen.X, actual.ActivatedScreen.X, "ActivatedScreen.X");
            Assert.AreEqual(expected.ActivatedScreen.Y, actual.ActivatedScreen.Y, "ActivatedScreen.Y");
            Assert.AreEqual(expected.ActivatedScreen.Width, actual.ActivatedScreen.Width, "ActivatedScreen.Width");
            Assert.AreEqual(expected.ActivatedScreen.Height, actual.ActivatedScreen.Height, "ActivatedScreen.Height");
            Assert.AreEqual(expected.ActivatedScreen.ToRectangle(), actual.ActivatedScreen.ToRectangle(), "ActivatedScreen.ToRectangle");
        }
    }

    [TestClass]
    public class GetJumpLocationTests
    {
        public class TestCase
        {
            public TestCase(PointInfo previewLocation, SizeInfo previewSize,  RectangleInfo desktopBounds, PointInfo expectedResult)
            {
                this.PreviewLocation = previewLocation;
                this.PreviewSize = previewSize;
                this.DesktopBounds = desktopBounds;
                this.ExpectedResult = expectedResult;
            }

            public PointInfo PreviewLocation { get; set; }

            public SizeInfo PreviewSize { get; set; }

            public RectangleInfo DesktopBounds { get; set; }

            public PointInfo ExpectedResult { get; set; }
        }

        public static IEnumerable<object[]> GetTestCases()
        {
            // corners and midpoint with a zero origin
            yield return new[] { new TestCase(new(0, 0), new(160, 120), new(0, 0, 1600, 1200), new(0, 0)) };
            yield return new[] { new TestCase(new(160, 0), new(160, 120), new(0, 0, 1600, 1200), new(1600, 0)) };
            yield return new[] { new TestCase(new(0, 120), new(160, 120), new(0, 0, 1600, 1200), new(0, 1200)) };
            yield return new[] { new TestCase(new(160, 120), new(160, 120), new(0, 0, 1600, 1200), new(1600, 1200)) };
            yield return new[] { new TestCase(new(80, 60), new(160, 120), new(0, 0, 1600, 1200), new(800, 600)) };

            // corners and midpoint with a positive origin
            yield return new[] { new TestCase(new(0, 0), new(160, 120), new(1000, 1000, 1600, 1200), new(1000, 1000)) };
            yield return new[] { new TestCase(new(160, 0), new(160, 120), new(1000, 1000, 1600, 1200), new(2600, 1000)) };
            yield return new[] { new TestCase(new(0, 120), new(160, 120), new(1000, 1000, 1600, 1200), new(1000, 2200)) };
            yield return new[] { new TestCase(new(160, 120), new(160, 120), new(1000, 1000, 1600, 1200), new(2600, 2200)) };
            yield return new[] { new TestCase(new(80, 60), new(160, 120), new(1000, 1000, 1600, 1200), new(1800, 1600)) };

            // corners and midpoint with a negative origin
            yield return new[] { new TestCase(new(0, 0), new(160, 120), new(-1000, -1000, 1600, 1200), new(-1000, -1000)) };
            yield return new[] { new TestCase(new(160, 0), new(160, 120), new(-1000, -1000, 1600, 1200), new(600, -1000)) };
            yield return new[] { new TestCase(new(0, 120), new(160, 120), new(-1000, -1000, 1600, 1200), new(-1000, 200)) };
            yield return new[] { new TestCase(new(160, 120), new(160, 120), new(-1000, -1000, 1600, 1200), new(600, 200)) };
            yield return new[] { new TestCase(new(80, 60), new(160, 120), new(-1000, -1000, 1600, 1200), new(-200, -400)) };
        }

        [TestMethod]
        [DynamicData(nameof(GetTestCases), DynamicDataSourceType.Method)]
        public void RunTestCases(TestCase data)
        {
            var actual = DrawingHelper.GetJumpLocation(
                data.PreviewLocation,
                data.PreviewSize,
                data.DesktopBounds);
            var expected = data.ExpectedResult;
            Assert.AreEqual(expected.X, actual.X);
            Assert.AreEqual(expected.Y, actual.Y);
        }
    }
}
