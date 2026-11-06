// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Imaging;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using MouseJump.Common.Capture;
using MouseJump.Common.Helpers;
using MouseJump.Models.Display;
using MouseJump.Models.Drawing;
using MouseJump.Models.Layout;
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

            var previewLayout = LayoutHelper.GetPreviewLayout(
                previewStyle: data.PreviewStyle,
                displayInfo: data.DisplayInfo,
                maximumSize: data.ActivatedScreen.DisplayArea.Size);

            // draw the preview image - the combined (border+background+bezels/screenshots)
            // render, since the expected golden images were captured against that. Production
            // code no longer flattens these into a single bitmap itself (screenshots/bezels
            // are laid out as native XAML elements by PreviewPane - see its remarks), so this
            // test composes them the same way for comparison purposes only, using the same
            // production rendering primitives (DrawingHelper.RenderBorder/RenderBackground,
            // and StaticScreenshotCaptureProvider for the screenshot content).
            var previewWindowStyle = LayoutHelper.GetPreviewWindowStyle(data.PreviewStyle.CanvasStyle);
            using var actual = await GetPreviewLayoutTests.ComposePreviewImageAsync(previewLayout, previewWindowStyle, desktopImage);

            var currentDirectory = Environment.CurrentDirectory;

            // save the actual image so we can pick it up as a build artifact
            var actualFilename = Path.Combine(
                currentDirectory,
                Path.GetFileNameWithoutExtension(data.ExpectedImageFilename) + "_actual" + Path.GetExtension(data.ExpectedImageFilename));
            actual.Save(actualFilename, ImageFormat.Png);

            // load the expected image
            var expected = GetPreviewLayoutTests.LoadImageResource(data.ExpectedImageFilename);

            // save the actual image so we can pick it up as a build artifact
            var expectedFilename = Path.Combine(
                currentDirectory,
                Path.GetFileNameWithoutExtension(data.ExpectedImageFilename) + "_expected" + Path.GetExtension(data.ExpectedImageFilename));
            expected.Save(expectedFilename, ImageFormat.Png);

            // compare the images
            AssertImagesEqual(expected, actual);
        }

        /// <summary>
        /// Composes the border, background, and per-screen bezels/screenshots for a
        /// <see cref="PreviewLayout"/> into a single flattened image, purely so this test can
        /// keep comparing against the existing golden images - production code renders these
        /// as separate layers/elements instead (see <see cref="PreviewLayout"/> and
        /// <c>FancyMouse.WinUI3.UI.PreviewPane</c>).
        /// </summary>
        private static async Task<Bitmap> ComposePreviewImageAsync(
            PreviewLayout previewLayout, BoxStyle hostBoxStyle, Bitmap desktopImage)
        {
            var canvasLayout = previewLayout.CanvasLayout;
            var hostBounds = LayoutHelper.GetPreviewWindowBounds(canvasLayout.CanvasBounds.OuterBounds, hostBoxStyle)
                .MoveTo(new PointInfo(0, 0));

            // hostBounds.OuterBounds is now (0,0)-based, so hostBounds.ContentBounds.Location
            // is exactly the margin+border offset to draw the (also zero-based)
            // canvas-relative background/bezels/screenshots at, within the combined bitmap.
            var offsetX = (int)hostBounds.ContentBounds.X;
            var offsetY = (int)hostBounds.ContentBounds.Y;

            // RenderBorder/RenderBackground are no longer box-aware - they just draw as large as
            // the size they're given, so it's on this caller to position the result at the right
            // box-model rectangle (BorderBounds/PaddingBounds/etc.) itself.
            var hostBorderBounds = hostBounds.BorderBounds;
            using var borderImage = DrawingHelper.RenderBorder(hostBorderBounds.Size, hostBoxStyle.BorderStyle);
            using var backgroundImage = DrawingHelper.RenderBackground(canvasLayout.CanvasBounds.OuterBounds.Size, canvasLayout.CanvasStyle.BackgroundStyle);

            var combinedBounds = hostBounds.OuterBounds.ToRectangle();
            var combinedImage = new Bitmap(combinedBounds.Width, combinedBounds.Height, PixelFormat.Format32bppPArgb);
            using var combinedGraphics = Graphics.FromImage(combinedImage);
            combinedGraphics.Clear(Color.Transparent);
            combinedGraphics.DrawImageUnscaled(borderImage, (int)hostBorderBounds.X, (int)hostBorderBounds.Y);
            combinedGraphics.DrawImageUnscaled(backgroundImage, offsetX, offsetY);

            var captureProvider = new StaticScreenshotCaptureProvider(desktopImage);
            foreach (var deviceLayout in canvasLayout.DeviceLayouts)
            {
                foreach (var screenLayout in deviceLayout.ScreenLayouts)
                {
                    // screens sit at a fractional (scaled) position within the canvas, unlike
                    // the canvas/host boxes above which are already zero-based - anchor at the
                    // *truncated* border position, matching how production now positions the
                    // bezel bitmap (see PreviewPane.CreateScreenSlot/PositionElement).
                    var screenBorderBounds = screenLayout.ScreenBounds.BorderBounds;
                    var anchorX = (int)screenBorderBounds.X;
                    var anchorY = (int)screenBorderBounds.Y;

                    using var bezelImage = DrawingHelper.RenderBorder(screenBorderBounds.Size, screenLayout.ScreenStyle.BorderStyle);
                    combinedGraphics.DrawImageUnscaled(
                        bezelImage,
                        offsetX + anchorX,
                        offsetY + anchorY);

                    using var screenshotImage = await captureProvider.CaptureAsync(
                        screenLayout.ScreenInfo.DisplayArea,
                        screenLayout.ScreenBounds.ContentBounds.Size);
                    combinedGraphics.DrawImageUnscaled(
                        screenshotImage,
                        offsetX + (int)screenLayout.ScreenBounds.ContentBounds.X,
                        offsetY + (int)screenLayout.ScreenBounds.ContentBounds.Y);
                }
            }

            return combinedImage;
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

                    // allow a 1-level-per-channel tolerance for residual GDI+ rasterization rounding.
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
