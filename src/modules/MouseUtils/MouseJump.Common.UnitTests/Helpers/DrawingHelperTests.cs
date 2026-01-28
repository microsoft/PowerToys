// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing.Imaging;
using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseJump.Common.Helpers;
using MouseJump.Common.Imaging;
using MouseJump.Common.Models.Drawing;
using MouseJump.Common.Models.Styles;

namespace MouseJump.Common.UnitTests.Helpers;

[TestClass]
public static class DrawingHelperTests
{
    [TestClass]
    public sealed class RenderPreviewTests
    {
        public TestContext TestContext { get; set; } = null!;

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
                    previewStyle: StyleHelper.BezelledPreviewStyle,
                    screens: new List<RectangleInfo>()
                    {
                        new(0, 0, 500, 500),
                        new(500, 0, 500, 500),
                        new(500, 500, 500, 500),
                        new(0, 500, 500, 500),
                    },
                    activatedLocation: new(x: 50, y: 50),
                    desktopImageFilename: "_test-4grid-desktop.png",
                    expectedImageFilename: "_test-4grid-expected.png"),
            };
            /* win 11 */
            yield return new object[]
            {
                new TestCase(
                    previewStyle: StyleHelper.BezelledPreviewStyle,
                    screens: new List<RectangleInfo>()
                    {
                        new(5120, 349, 1920, 1080),
                        new(0, 0, 5120, 1440),
                    },
                    activatedLocation: new(x: 50, y: 50),
                    desktopImageFilename: "_test-win11-desktop.png",
                    expectedImageFilename: "_test-win11-expected.png"),
            };
        }

        [TestMethod]
        [DynamicData(nameof(GetTestCases), DynamicDataSourceType.Method)]
        public void RunTestCases(TestCase data)
        {
            // load the fake desktop image
            using var desktopImage = RenderPreviewTests.LoadImageResource(data.DesktopImageFilename);

            // draw the preview image
            var previewLayout = LayoutHelper.GetPreviewLayout(
                previewStyle: data.PreviewStyle,
                screens: data.Screens,
                activatedLocation: data.ActivatedLocation);
            var imageCopyService = new StaticImageRegionCopyService(desktopImage);
            using var actual = DrawingHelper.RenderPreview(previewLayout, imageCopyService);

            // load the expected image
            var expected = RenderPreviewTests.LoadImageResource(data.ExpectedImageFilename);

            // compare the images
            AssertImagesEqual(NormalizeFormat(expected), NormalizeFormat(actual));
        }

        private static Bitmap LoadImageResource(string filename)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyName = new AssemblyName(assembly.FullName ?? throw new InvalidOperationException());
            var resourceName = $"{typeof(DrawingHelperTests).Namespace}.{filename.Replace("/", ".")}";
            var resourceNames = assembly.GetManifestResourceNames();
            if (!resourceNames.Contains(resourceName))
            {
                var message = new StringBuilder();
                message.AppendLine(CultureInfo.InvariantCulture, $"Embedded resource '{resourceName}' does not exist.");
                message.AppendLine($"Known resources:");
                foreach (var name in resourceNames)
                {
                    message.AppendLine(name);
                }

                throw new InvalidOperationException(message.ToString());
            }

            var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException();
            var image = (Bitmap)Image.FromStream(stream);
            return image;
        }

        /// <summary>
        /// Naive / brute force image comparison - we can optimize this later :-)
        /// </summary>
        private void AssertImagesEqual(Bitmap expected, Bitmap actual)
        {
            if (ImagesAreEqual(expected, actual))
            {
                return;
            }

            var outputDir = Path.Combine(TestContext.ResultsDirectory!, TestContext.TestName!);

            Directory.CreateDirectory(outputDir);

            var expectedPath = Path.Combine(outputDir, "expected.png");
            var actualPath = Path.Combine(outputDir, "actual.png");
            var diffPath = Path.Combine(outputDir, "diff.png");

            expected.Save(expectedPath, ImageFormat.Png);
            actual.Save(actualPath, ImageFormat.Png);

            using var diff = CreateDiffBitmap(expected, actual);
            diff.Save(diffPath, ImageFormat.Png);

            TestContext.AddResultFile(expectedPath);
            TestContext.AddResultFile(actualPath);
            TestContext.AddResultFile(diffPath);

            Assert.Fail($"Images differ. Artifacts saved to {outputDir}");
        }

        private static bool ImagesAreEqual(Bitmap expected, Bitmap actual)
        {
            if (expected.Width != actual.Width ||
                expected.Height != actual.Height)
            {
                return false;
            }

            for (var y = 0; y < expected.Height; y++)
            {
                for (var x = 0; x < expected.Width; x++)
                {
                    var e = expected.GetPixel(x, y);
                    var a = actual.GetPixel(x, y);

                    if (Math.Abs(e.A - a.A) > 1 ||
                        Math.Abs(e.R - a.R) > 1 ||
                        Math.Abs(e.G - a.G) > 1 ||
                        Math.Abs(e.B - a.B) > 1)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static Bitmap CreateDiffBitmap(Bitmap expected, Bitmap actual)
        {
            var width = expected.Width;
            var height = expected.Height;

            var diff = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var e = expected.GetPixel(x, y);
                    var a = actual.GetPixel(x, y);

                    var different =
                        Math.Abs(e.A - a.A) > 1 ||
                        Math.Abs(e.R - a.R) > 1 ||
                        Math.Abs(e.G - a.G) > 1 ||
                        Math.Abs(e.B - a.B) > 1;

                    diff.SetPixel(
                        x,
                        y,
                        different ? Color.Magenta : Color.FromArgb(80, e));
                }
            }

            return diff;
        }

        private static Bitmap NormalizeFormat(Bitmap src)
        {
            // Ensure a predictable format.
            var dst = new Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(dst);
            g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            g.DrawImageUnscaled(src, 0, 0);
            return dst;
        }

        private static Bitmap FlattenOnBackground(Bitmap src, Color bg)
        {
            var dst = new Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(dst);
            g.Clear(bg);
            g.DrawImageUnscaled(src, 0, 0);
            return dst;
        }
    }
}
