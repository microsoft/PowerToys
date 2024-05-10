// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseJumpUI.Common.Helpers;
using MouseJumpUI.Common.Imaging;
using MouseJumpUI.Common.Models.Drawing;
using MouseJumpUI.Common.Models.Styles;
using MouseJumpUI.Helpers;

namespace MouseJumpUI.UnitTests.Common.Helpers;

[TestClass]
public static class DrawingHelperTests
{
    [TestClass]
    public sealed class GetPreviewLayoutTests
    {
        public sealed class TestCase
        {
            public TestCase(PreviewStyle previewStyle, List<RectangleInfo> screens, PointInfo activatedLocation, string desktopImageFilename, string expectedImageFilename)
            {
                this.PreviewStyle = previewStyle;
                this.Screens = screens;
                this.ActivatedLocation = activatedLocation;
                this.DesktopImageFilename = desktopImageFilename;
                this.ExpectedImageFilename = expectedImageFilename;
            }

            public PreviewStyle PreviewStyle { get; }

            public List<RectangleInfo> Screens { get; }

            public PointInfo ActivatedLocation { get; }

            public string DesktopImageFilename { get; }

            public string ExpectedImageFilename { get; }
        }

        public static IEnumerable<object[]> GetTestCases()
        {
            /* 4-grid */
            yield return new object[]
            {
                new TestCase(
                    previewStyle: StyleHelper.DefaultPreviewStyle,
                    screens: new List<RectangleInfo>()
                    {
                        new(0, 0, 500, 500),
                        new(500, 0, 500, 500),
                        new(500, 500, 500, 500),
                        new(0, 500, 500, 500),
                    },
                    activatedLocation: new(x: 50, y: 50),
                    desktopImageFilename: "Common/Helpers/_test-4grid-desktop.png",
                    expectedImageFilename: "Common/Helpers/_test-4grid-expected.png"),
            };
            /* win 11 */
            yield return new object[]
            {
                new TestCase(
                    previewStyle: StyleHelper.DefaultPreviewStyle,
                    screens: new List<RectangleInfo>()
                    {
                        new(5120, 349, 1920, 1080),
                        new(0, 0, 5120, 1440),
                    },
                    activatedLocation: new(x: 50, y: 50),
                    desktopImageFilename: "Common/Helpers/_test-win11-desktop.png",
                    expectedImageFilename: "Common/Helpers/_test-win11-expected.png"),
            };
        }

        [TestMethod]
        [DynamicData(nameof(GetTestCases), DynamicDataSourceType.Method)]
        public void RunTestCases(TestCase data)
        {
            // load the fake desktop image
            using var desktopImage = GetPreviewLayoutTests.LoadImageResource(data.DesktopImageFilename);

            // draw the preview image
            var previewLayout = LayoutHelper.GetPreviewLayout(
                previewStyle: data.PreviewStyle,
                screens: data.Screens,
                activatedLocation: data.ActivatedLocation);
            var imageCopyService = new StaticImageRegionCopyService(desktopImage);
            using var actual = DrawingHelper.RenderPreview(previewLayout, imageCopyService);

            // load the expected image
            var expected = GetPreviewLayoutTests.LoadImageResource(data.ExpectedImageFilename);

            // compare the images
            var screens = System.Windows.Forms.Screen.AllScreens;
            AssertImagesEqual(expected, actual);
        }

        private static Bitmap LoadImageResource(string filename)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyName = new AssemblyName(assembly.FullName ?? throw new InvalidOperationException());
            var resourceName = $"Microsoft.{assemblyName.Name}.{filename.Replace("/", ".")}";
            var resourceNames = assembly.GetManifestResourceNames();
            if (!resourceNames.Contains(resourceName))
            {
                throw new InvalidOperationException($"Embedded resource '{resourceName}' does not exist.");
            }

            var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException();
            var image = (Bitmap)Image.FromStream(stream);
            return image;
        }

        /// <summary>
        /// Naive / brute force image comparison - we can optimise this later :-)
        /// </summary>
        private static void AssertImagesEqual(Bitmap expected, Bitmap actual)
        {
            Assert.AreEqual(
                expected.Width,
                actual.Width,
                $"expected width: {expected.Width}, actual width: {actual.Width}");
            Assert.AreEqual(
                expected.Height,
                actual.Height,
                $"expected height: {expected.Height}, actual height: {actual.Height}");
            for (var y = 0; y < expected.Height; y++)
            {
                for (var x = 0; x < expected.Width; x++)
                {
                    var expectedPixel = expected.GetPixel(x, y);
                    var actualPixel = actual.GetPixel(x, y);

                    // allow a small tolerance for rounding differences in gdi
                    Assert.IsTrue(
                        (Math.Abs(expectedPixel.A - actualPixel.A) <= 1) &&
                        (Math.Abs(expectedPixel.R - actualPixel.R) <= 1) &&
                        (Math.Abs(expectedPixel.G - actualPixel.G) <= 1) &&
                        (Math.Abs(expectedPixel.B - actualPixel.B) <= 1),
                        $"images differ at pixel ({x}, {y}) - expected: {expectedPixel}, actual: {actualPixel}");
                }
            }
        }
    }
}
