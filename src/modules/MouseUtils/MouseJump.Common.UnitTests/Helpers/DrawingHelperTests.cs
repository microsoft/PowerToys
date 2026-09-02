// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using MouseJump.Common.Helpers;
using MouseJump.Common.Imaging;
using MouseJump.Models.Display;
using MouseJump.Models.Drawing;
using MouseJump.Models.Styles;

namespace MouseJump.Common.UnitTests.Helpers;

public static class DrawingHelperTests
{
    [TestClass]
    public sealed class GetPreviewLayoutTests
    {
        public sealed class TestCase
        {
            public TestCase(PreviewStyle previewStyle, DisplayInfo displayInfo, ScreenInfo activatedScreen, PointInfo activatedLocation, string desktopImageFilename, string expectedImageFilename)
            {
                this.PreviewStyle = previewStyle;
                this.DisplayInfo = displayInfo;
                this.ActivatedScreen = activatedScreen;
                this.ActivatedLocation = activatedLocation;
                this.DesktopImageFilename = desktopImageFilename;
                this.ExpectedImageFilename = expectedImageFilename;
            }

            public PreviewStyle PreviewStyle { get; }

            public DisplayInfo DisplayInfo { get; }

            public ScreenInfo ActivatedScreen { get; }

            public PointInfo ActivatedLocation { get; }

            public string DesktopImageFilename { get; }

            public string ExpectedImageFilename { get; }
        }

        public static IEnumerable<object[]> GetTestCases()
        {
            // colors like "SystemColors.Highlight" can change between versions of windows,
            // so we'll use hard-coded colors instead to reduce false test failures
            var systemHighlight = Color.FromArgb(0, 120, 215);
            var previewStyle = StyleHelper.BezelledPreviewStyle;
            previewStyle = new(
                canvasSize: previewStyle.CanvasSize,
                canvasStyle: new(
                    marginStyle: previewStyle.CanvasStyle.MarginStyle,
                    borderStyle: previewStyle.CanvasStyle.BorderStyle.WithColor(systemHighlight),
                    paddingStyle: previewStyle.CanvasStyle.PaddingStyle,
                    backgroundStyle: previewStyle.CanvasStyle.BackgroundStyle
                ),
                screenStyle: previewStyle.ScreenStyle,
                extraColors: previewStyle.ExtraColors
            );

            // 4-grid
            var displayInfo = new DisplayInfo(
                devices: new DeviceInfo[]
                {
                    new(
                        hostname: "localhost",
                        localhost: true,
                        screens: new List<ScreenInfo>
                        {
                            new(
                                handle: 0,
                                primary: true,
                                displayArea: new(0, 0, 500, 500),
                                workingArea: new(0, 0, 500, 500)),
                            new(
                                handle: 0,
                                primary: false,
                                displayArea: new(500, 0, 500, 500),
                                workingArea: new(500, 0, 500, 500)),
                            new(
                                handle: 0,
                                primary: false,
                                displayArea: new(500, 500, 500, 500),
                                workingArea: new(500, 500, 500, 500)),
                            new(
                                handle: 0,
                                primary: false,
                                displayArea: new(0, 500, 500, 500),
                                workingArea: new(0, 500, 500, 500)),
                        }
                    ),
                });
            yield return new object[]
            {
                new TestCase(
                    previewStyle: previewStyle,
                    displayInfo: displayInfo,
                    activatedScreen: displayInfo.Devices[0].Screens[0],
                    activatedLocation: new(x: 50, y: 50),
                    desktopImageFilename: "_test-4grid-desktop.png",
                    expectedImageFilename: "_test-4grid-expected.png"),
            };

            // win 11
            displayInfo = new(
                devices: new DeviceInfo[]
                {
                    new(
                        hostname: "localhost",
                        localhost: true,
                        screens: new List<ScreenInfo>
                        {
                            new(
                                handle: 0,
                                primary: true,
                                displayArea: new(5120, 349, 1920, 1080),
                                workingArea: new(5120, 349, 1920, 1080)),
                            new(
                                handle: 0,
                                primary: false,
                                displayArea: new(0, 0, 5120, 1440),
                                workingArea: new(0, 0, 5120, 1440)),
                        }
                    ),
                }
            );
            yield return new object[]
            {
                new TestCase(
                    previewStyle: previewStyle,
                    displayInfo: displayInfo,
                    activatedScreen: displayInfo.Devices[0].Screens[0],
                    activatedLocation: new(x: 50, y: 50),
                    desktopImageFilename: "_test-win11-desktop.png",
                    expectedImageFilename: "_test-win11-expected.png"),
            };
        }

        [TestMethod]
        [DynamicData(nameof(GetTestCases))]
        public async Task RunTestCases(TestCase data)
        {
            // load the fake desktop image
            using var desktopImage = GetPreviewLayoutTests.LoadImageResource(data.DesktopImageFilename);

            var formLayout = LayoutHelper.GetFormLayout(
                previewStyle: data.PreviewStyle,
                displayInfo: data.DisplayInfo,
                activatedScreen: data.ActivatedScreen,
                activatedLocation: data.ActivatedLocation);

            var imageCopyServices = formLayout.CanvasLayout.DeviceLayouts
                .Select(
                    deviceLayout => (IImageRegionCopyService)new StaticImageRegionCopyService(desktopImage))
                .ToList();

            // draw the preview image
            using var actual = await DrawingHelper.RenderPreviewAsync(formLayout.CanvasLayout, data.ActivatedScreen, imageCopyServices);

            // save the actual image so we can pick it up as a build artifact
            var actualFilename = Path.GetFileNameWithoutExtension(data.ExpectedImageFilename) + "_actual" + Path.GetExtension(data.ExpectedImageFilename);
            actual.Save(actualFilename, ImageFormat.Png);

            // load the expected image
            var expected = GetPreviewLayoutTests.LoadImageResource(data.ExpectedImageFilename);

            // save the actual image so we can pick it up as a build artifact
            var expectedFilename = Path.GetFileNameWithoutExtension(data.ExpectedImageFilename) + "_expected" + Path.GetExtension(data.ExpectedImageFilename);
            expected.Save(expectedFilename, ImageFormat.Png);

            // compare the images
            AssertImagesEqual(expected, actual);
        }

        private static Bitmap LoadImageResource(string filename)
        {
            var referenceType = typeof(DrawingHelperTests);
            var resourceAssembly = referenceType.Assembly;

            var resourcePrefix = referenceType.Namespace;
            var resourceName = $"{resourcePrefix}.{filename.Replace("/", ".")}";
            var resourceNames = resourceAssembly.GetManifestResourceNames();
            if (!resourceNames.Contains(resourceName))
            {
                var message = string.Join(
                    Environment.NewLine,
                    new List<string>([
                        $"Embedded resource '{resourceName}' does not exist.",
                        string.Empty,
                        "Valid resource names are:",
                        string.Empty])
                    .Concat(resourceNames));
                throw new InvalidOperationException(message);
            }

            var stream = resourceAssembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException();
            var image = (Bitmap)Image.FromStream(stream);
            return image;
        }

        /// <summary>
        /// Naive / brute force image comparison - we can optimize this later :-)
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
