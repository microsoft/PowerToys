// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Drawing;
using System.Text.Json;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using MouseJump.Common.Helpers;
using MouseJump.Models.Display;
using MouseJump.Models.Drawing;
using MouseJump.Models.Styles;
using MouseJump.Models.ViewModel;

namespace MouseJump.Common.UnitTests.Helpers;

public static class LayoutHelperTests
{
    /*
    [TestClass]
    public sealed class OldLayoutTests
    {

        public static IEnumerable<object[]> GetTestCases()
        {
            // check we handle rounding errors in scaling the preview form
            // that might make the form *larger* than the current screen -
            // e.g. a desktop 7168 x 1440 scaled to a screen 1024 x 768
            // with a 5px form padding border:
            //
            // ((decimal)1014 / 7168) * 7168 = 1014.0000000000000000000000002
            //
            // +----------------+
            // |                |
            // |       1        +-------+
            // |                |   0   |
            // +----------------+-------+
            layoutConfig = new LayoutConfig(
                virtualScreenBounds: new(0, 0, 7168, 1440),
                screens: new List<ScreenInfo>
                {
                    new(HMONITOR.Null, false, new(6144, 0, 1024, 768), new(6144, 0, 1024, 768)),
                    new(HMONITOR.Null, false, new(0, 0, 6144, 1440), new(0, 0, 6144, 1440)),
                },
                activatedLocation: new(6656, 384),
                activatedScreenIndex: 0,
                activatedScreenNumber: 1,
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
                activatedScreenBounds: new(6144, 0, 1024, 768));
            yield return new object[] { new TestCase(layoutConfig, layoutInfo) };

            // check we handle rounding errors in scaling the preview form
            // that might make the form one pixel *smaller* than the current screen -
            // e.g. a desktop 7168 x 1440 scaled to a screen 1024 x 768
            // with a 5px form padding border:
            //
            // ((decimal)1280 / 7424) * 7424 = 1279.9999999999999999999999999
            //
            // +----------------+
            // |                |
            // |       1        +-------+
            // |                |   0   |
            // +----------------+-------+
            layoutConfig = new LayoutConfig(
                virtualScreenBounds: new(0, 0, 7424, 1440),
                screens: new List<ScreenInfo>
                {
                    new(HMONITOR.Null, false, new(6144, 0, 1280, 768), new(6144, 0, 1280, 768)),
                    new(HMONITOR.Null, false, new(0, 0, 6144, 1440), new(0, 0, 6144, 1440)),
                },
                activatedLocation: new(6784, 384),
                activatedScreenIndex: 0,
                activatedScreenNumber: 1,
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
                activatedScreenBounds: new(6144, 0, 1280, 768));
            yield return new object[] { new TestCase(layoutConfig, layoutInfo) };
        }
    }
    */

    [TestClass]
    public sealed class GetPreviewLayoutTests
    {
        public sealed class TestCase
        {
            public TestCase(string testName, PreviewStyle previewStyle, DisplayInfo displayInfo, ScreenInfo activatedScreen, PointInfo activatedLocation, FormViewModel expectedResult)
            {
                this.TestName = testName;
                this.PreviewStyle = previewStyle;
                this.DisplayInfo = displayInfo;
                this.ActivatedLocation = activatedLocation;
                this.ActivatedScreen = activatedScreen;
                this.ExpectedResult = expectedResult;
            }

            public string TestName { get; }

            public PreviewStyle PreviewStyle { get; }

            public DisplayInfo DisplayInfo { get; }

            public ScreenInfo ActivatedScreen { get; }

            public PointInfo ActivatedLocation { get; }

            public FormViewModel ExpectedResult { get; }
        }

        public static IEnumerable<object[]> GetTestCases()
        {
            // happy path - single device with screen and 50% scaling,
            // *has* a preview border but *no* screenshot borders
            //
            // +----------------+
            // |                |
            // |       0        |
            // |                |
            // +----------------+
            var testName = "Test 1 - Happy Path";
            var previewStyle = new PreviewStyle(
                canvasSize: new(
                    width: (1024 / 2) + (5 * 2) + (1 * 2), // half the screen size, plus additional room for canvas border and padding
                    height: (768 / 2) + (5 * 2) + (1 * 2) // half the screen size, plus additional room for canvas border and padding
                ),
                canvasStyle: new(
                    marginStyle: MarginStyle.Empty,
                    borderStyle: new(
                        color: SystemColors.Highlight,
                        all: 5,
                        depth: 3),
                    paddingStyle: new(
                        all: 1),
                    backgroundStyle: new(
                        color1: Color.FromArgb(13, 87, 210), // light blue
                        color2: Color.FromArgb(3, 68, 192) // darker blue
                    )
                ),
                screenStyle: BoxStyle.Empty,
                extraColors: Array.Empty<Color>());
            var displayInfo = new DisplayInfo(
                devices: new List<DeviceInfo>
                {
                    new(
                        hostname: "localhost",
                        localhost: true,
                        screens: new List<ScreenInfo>
                        {
                            new(
                                handle: 0,
                                primary: true,
                                displayArea: new(0, 0, 1024, 768),
                                workingArea: new(0, 0, 1024, 768)),
                        }
                    ),
                });
            var activatedScreen = displayInfo.Devices[0].Screens[0];
            var activatedLocation = activatedScreen.DisplayArea.Midpoint;
            var expectedResult = new FormViewModel(
                formBounds: new(250, 186, 524, 396),
                canvasLayout: new(
                    canvasBounds: BoxBounds.CreateFromOuterBounds(
                        outerBounds: new(0, 0, 524, 396),
                        boxStyle: previewStyle.CanvasStyle),
                    canvasStyle: previewStyle.CanvasStyle,
                    deviceLayouts: new List<DeviceViewModel>()
                    {
                        new(
                            deviceInfo: displayInfo.Devices[0],
                            deviceBounds: BoxBounds.CreateFromOuterBounds(
                                outerBounds: new(6, 6, 512, 384),
                                boxStyle: BoxStyle.Empty),
                            deviceStyle: BoxStyle.Empty,
                            screenLayouts: new List<ScreenViewModel>
                            {
                                new(
                                    screenInfo: displayInfo.Devices[0].Screens[0],
                                    screenBounds: BoxBounds.CreateFromOuterBounds(
                                        outerBounds: new(6, 6, 512, 384),
                                        boxStyle: previewStyle.ScreenStyle),
                                    screenStyle: previewStyle.ScreenStyle),
                            }
                        ),
                    }
                ));
            yield return new object[] { new TestCase(testName, previewStyle, displayInfo, activatedScreen, activatedLocation, expectedResult) };

            // happy path - single device with screen and 50% scaling,
            // *no* preview borders but *has* screenshot borders
            //
            // +----------------+
            // |                |
            // |       0        |
            // |                |
            // +----------------+
            testName = "Test 2 - Happy Path";
            previewStyle = new PreviewStyle(
                canvasSize: new(
                    width: 1024 / 2, // half the screen size
                    height: 768 / 2 // half the screen size
                ),
                canvasStyle: BoxStyle.Empty,
                screenStyle: new(
                    marginStyle: new(
                        all: 1),
                    borderStyle: new(
                        color: SystemColors.Highlight,
                        all: 5,
                        depth: 3),
                    paddingStyle: PaddingStyle.Empty,
                    backgroundStyle: new(
                        color1: Color.FromArgb(13, 87, 210), // light blue
                        color2: Color.FromArgb(3, 68, 192) // darker blue
                    )),
                extraColors: Array.Empty<Color>());
            displayInfo = new DisplayInfo(
                devices: new List<DeviceInfo>
                {
                    new(
                        hostname: "localhost",
                        localhost: true,
                        screens: new List<ScreenInfo>
                        {
                            new(
                                handle: 0,
                                primary: true,
                                displayArea: new(0, 0, 1024, 768),
                                workingArea: new(0, 0, 1024, 768)),
                        }
                    ),
                });
            activatedScreen = displayInfo.Devices[0].Screens[0];
            activatedLocation = activatedScreen.DisplayArea.Midpoint;
            expectedResult = new FormViewModel(
                formBounds: new(256, 192, 512, 384),
                canvasLayout: new(
                    canvasBounds: BoxBounds.CreateFromOuterBounds(
                        outerBounds: new(0, 0, 512, 384),
                        boxStyle: previewStyle.CanvasStyle),
                    canvasStyle: previewStyle.CanvasStyle,
                    deviceLayouts: new List<DeviceViewModel>()
                    {
                        new(
                            deviceInfo: displayInfo.Devices[0],
                            deviceBounds: BoxBounds.CreateFromOuterBounds(
                                outerBounds: new(0, 0, 512, 384),
                                boxStyle: BoxStyle.Empty),
                            deviceStyle: BoxStyle.Empty,
                            screenLayouts: new List<ScreenViewModel>
                            {
                                new(
                                    screenInfo: displayInfo.Devices[0].Screens[0],
                                    screenBounds: BoxBounds.CreateFromOuterBounds(
                                        outerBounds: new(0, 0, 512, 384),
                                        boxStyle: previewStyle.ScreenStyle),
                                    screenStyle: previewStyle.ScreenStyle),
                            }
                        ),
                    }
                ));
            yield return new object[] { new TestCase(testName, previewStyle, displayInfo, activatedScreen, activatedLocation, expectedResult) };

            // rounding error check - single screen with 33% scaling,
            // no borders, check to make sure form scales to exactly
            // fill the canvas size with no rounding errors.
            //
            // in this test the preview width is 300 and the desktop is
            // 900, so the scaling factor is 1/3, but this gets rounded
            // to 0.3333333333333333333333333333, and 900 times this value
            // is 299.99999999999999999999999997. if we don't scale correctly
            // the resulting form width might only be 299 pixels instead of 300
            //
            // +----------------+
            // |                |
            // |       0        |
            // |                |
            // +----------------+
            testName = "Test 3 - Rounding Error Check";
            previewStyle = new PreviewStyle(
                canvasSize: new(
                    width: 300,
                    height: 200
                ),
                canvasStyle: BoxStyle.Empty,
                screenStyle: BoxStyle.Empty,
                extraColors: Array.Empty<Color>());
            displayInfo = new DisplayInfo(
                devices: new List<DeviceInfo>
                {
                    new(
                        hostname: "localhost",
                        localhost: true,
                        screens: new List<ScreenInfo>
                        {
                            new(
                                handle: 0,
                                primary: true,
                                displayArea: new(0, 0, 900, 200),
                                workingArea: new(0, 0, 900, 200)),
                        }
                    ),
                });
            activatedScreen = displayInfo.Devices[0].Screens[0];
            activatedLocation = activatedScreen.DisplayArea.Midpoint;
            expectedResult = new FormViewModel(
                formBounds: new(300, 66.5m, 300, 67),
                canvasLayout: new(
                    canvasBounds: BoxBounds.CreateFromOuterBounds(
                        outerBounds: new(0, 0, 300, 67),
                        boxStyle: previewStyle.CanvasStyle),
                    canvasStyle: previewStyle.CanvasStyle,
                    deviceLayouts: new List<DeviceViewModel>()
                    {
                        new(
                            deviceInfo: displayInfo.Devices[0],
                            deviceBounds: BoxBounds.CreateFromOuterBounds(
                                outerBounds: new(0, 0, 300, 67),
                                boxStyle: BoxStyle.Empty),
                            deviceStyle: BoxStyle.Empty,
                            screenLayouts: new List<ScreenViewModel>()
                            {
                                new(
                                    screenInfo: displayInfo.Devices[0].Screens[0],
                                    screenBounds: BoxBounds.CreateFromOuterBounds(
                                        outerBounds: new(0, 0, 300, 67),
                                        boxStyle: previewStyle.ScreenStyle),
                                    screenStyle: previewStyle.ScreenStyle),
                            }
                        ),
                    }
                ));
            yield return new object[] { new TestCase(testName, previewStyle, displayInfo, activatedScreen, activatedLocation, expectedResult) };

            // primary monitor not topmost / leftmost - if there are screens
            // that are further left or higher up than the primary monitor
            // they'll have negative coordinates which has caused some
            // issues with calculations in the past. this test will make
            // sure we handle screens with negative coordinates gracefully
            //
            // +-------+
            // |   0   +----------------+
            // +-------+                |
            //         |       1        |
            //         |                |
            //         +----------------+
            testName = "Test 4 - Negative Coordinates";
            previewStyle = new PreviewStyle(
                canvasSize: new(
                    width: 716,
                    height: 204
                ),
                canvasStyle: new(
                    marginStyle: MarginStyle.Empty,
                    borderStyle: new(
                        color: SystemColors.Highlight,
                        all: 5,
                        depth: 3),
                    paddingStyle: new(
                        all: 1),
                    backgroundStyle: new(
                        color1: Color.FromArgb(13, 87, 210), // light blue
                        color2: Color.FromArgb(3, 68, 192) // darker blue
                    )
                ),
                screenStyle: new(
                    marginStyle: new(
                        all: 1),
                    borderStyle: new(
                        color: SystemColors.Highlight,
                        all: 5,
                        depth: 3),
                    paddingStyle: PaddingStyle.Empty,
                    backgroundStyle: new(
                        color1: Color.FromArgb(13, 87, 210), // light blue
                        color2: Color.FromArgb(3, 68, 192) // darker blue
                    )),
                extraColors: Array.Empty<Color>());
            displayInfo = new DisplayInfo(
                devices: new List<DeviceInfo>
                {
                    new(
                        hostname: "localhost",
                        localhost: true,
                        screens: new List<ScreenInfo>
                        {
                            new(
                                handle: 0,
                                primary: true,
                                displayArea: new(-1920, -480, 1920, 1080),
                                workingArea: new(-1920, -480, 1920, 1080)),
                            new(
                                handle: 0,
                                primary: false,
                                displayArea: new(0, 0, 5120, 1440),
                                workingArea: new(0, 0, 5120, 1440)),
                        }
                    ),
                });
            activatedScreen = displayInfo.Devices[0].Screens[0];
            activatedLocation = activatedScreen.DisplayArea.Midpoint;
            expectedResult = new FormViewModel(
                formBounds: new(-1318, -42, 716, 204),
                canvasLayout: new(
                    canvasBounds: BoxBounds.CreateFromOuterBounds(
                        outerBounds: new(0, 0, 716, 204),
                        boxStyle: previewStyle.CanvasStyle),
                    canvasStyle: previewStyle.CanvasStyle,
                    deviceLayouts: new List<DeviceViewModel>()
                    {
                        new(
                            deviceInfo: displayInfo.Devices[0],
                            deviceBounds: BoxBounds.CreateFromOuterBounds(
                                outerBounds: new(6, 6, 704, 192),
                                boxStyle: BoxStyle.Empty),
                            deviceStyle: BoxStyle.Empty,
                            screenLayouts: new List<ScreenViewModel>()
                            {
                                new(
                                    screenInfo: displayInfo.Devices[0].Screens[0],
                                    screenBounds: BoxBounds.CreateFromOuterBounds(
                                        outerBounds: new(6, 6, 192, 108),
                                        boxStyle: previewStyle.ScreenStyle),
                                    screenStyle: previewStyle.ScreenStyle),
                                new(
                                    screenInfo: displayInfo.Devices[0].Screens[1],
                                    screenBounds: BoxBounds.CreateFromOuterBounds(
                                        outerBounds: new(198, 54, 512, 144),
                                        boxStyle: previewStyle.ScreenStyle),
                                    screenStyle: previewStyle.ScreenStyle),
                            }
                        ),
                    }
                ));
            yield return new object[] { new TestCase(testName, previewStyle, displayInfo, activatedScreen, activatedLocation, expectedResult) };

            // two devices side-by-side with a single screen each
            //
            //      device 1            device 2
            // +----------------+  +----------------+
            // |                |  |                |
            // |       0        |  |       0        |
            // |                |  |                |
            // +----------------+  +----------------+
            testName = "Test 5 - Two Devices Side-by-Side";
            previewStyle = new PreviewStyle(
                canvasSize: new(
                    width: 1600,
                    height: 1200
                ),
                canvasStyle: BoxStyle.Empty,
                screenStyle: BoxStyle.Empty,
                extraColors: Array.Empty<Color>());
            displayInfo = new DisplayInfo(
                devices: new List<DeviceInfo>
                {
                    new(
                        hostname: "localhost",
                        localhost: true,
                        screens: new List<ScreenInfo>
                        {
                            new(
                                handle: 0,
                                primary: true,
                                displayArea: new(0, 0, 5120, 1440),
                                workingArea: new(0, 0, 5120, 1440)),
                        }
                    ),
                    new(
                        hostname: "remotehost",
                        localhost: false,
                        screens: new List<ScreenInfo>
                        {
                            new(
                                handle: 0,
                                primary: true,
                                displayArea: new(0, 0, 5120, 1440),
                                workingArea: new(0, 0, 5120, 1440)),
                        }
                    ),
                });
            activatedScreen = displayInfo.Devices[0].Screens[0];
            activatedLocation = activatedScreen.DisplayArea.Midpoint;
            expectedResult = new FormViewModel(
                formBounds: new(1760, 607.5m, 1600, 225),
                canvasLayout: new(
                    canvasBounds: BoxBounds.CreateFromOuterBounds(
                        outerBounds: new(0, 0, 1600, 225),
                        boxStyle: previewStyle.CanvasStyle),
                    canvasStyle: previewStyle.CanvasStyle,
                    deviceLayouts: new List<DeviceViewModel>()
                    {
                        new(
                            deviceInfo: displayInfo.Devices[0],
                            deviceBounds: BoxBounds.CreateFromOuterBounds(
                                outerBounds: new(0, 0, 800, 225),
                                boxStyle: BoxStyle.Empty),
                            deviceStyle: BoxStyle.Empty,
                            screenLayouts: new List<ScreenViewModel>()
                            {
                                new(
                                    screenInfo: displayInfo.Devices[0].Screens[0],
                                    screenBounds: BoxBounds.CreateFromOuterBounds(
                                        outerBounds: new(0, 0, 800, 225),
                                        boxStyle: previewStyle.ScreenStyle),
                                    screenStyle: previewStyle.ScreenStyle),
                            }
                        ),
                        new(
                            deviceInfo: displayInfo.Devices[1],
                            deviceBounds: BoxBounds.CreateFromOuterBounds(
                                outerBounds: new(800, 0, 800, 225),
                                boxStyle: BoxStyle.Empty),
                            deviceStyle: BoxStyle.Empty,
                            screenLayouts: new List<ScreenViewModel>()
                            {
                                new(
                                    screenInfo: displayInfo.Devices[1].Screens[0],
                                    screenBounds: BoxBounds.CreateFromOuterBounds(
                                        outerBounds: new(800, 0, 800, 225),
                                        boxStyle: previewStyle.ScreenStyle),
                                    screenStyle: previewStyle.ScreenStyle),
                            }
                        ),
                    }
                ));
            yield return new object[] { new TestCase(testName, previewStyle, displayInfo, activatedScreen, activatedLocation, expectedResult) };

            // TODO: add a test to make sure the form is nudged into the bounds
            // of the screen if it's activated near an edge or corner
        }

        [TestMethod]
        [DynamicData(nameof(GetTestCases))]
        public void RunTestCases(TestCase data)
        {
            // note - even if values are within 0.0001M of each other they could
            // still round to different values - e.g.
            // (int)1279.999999999999 -> 1279
            // vs
            // (int)1280.000000000000 -> 1280
            // so we'll compare the raw values, *and* convert to an int-based
            // Rectangle to compare rounded values
            var actual = LayoutHelper.GetFormLayout(data.PreviewStyle, data.DisplayInfo, data.ActivatedScreen, data.ActivatedLocation);
            var expected = data.ExpectedResult;
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
            };
            var actualJson = JsonSerializer.Serialize(actual, options);
            var expectedJson = JsonSerializer.Serialize(expected, options);
            Assert.AreEqual(expectedJson, actualJson);
        }

        /// <summary>
        /// Basic performance test just to avoid any massive regressions.
        /// </summary>
        [TestMethod]
        [Ignore("Ignore on CI runners - only run locally")]
        public void BasicPerformanceTest()
        {
            // primary monitor not topmost / leftmost - if there are screens
            // that are further left or higher up than the primary monitor
            // they'll have negative coordinates which has caused some
            // issues with calculations in the past. this test will make
            // sure we handle screens with negative coordinates gracefully
            //
            // +-------+
            // |   0   +----------------+
            // +-------+                |
            //         |       1        |
            //         |                |
            //         +----------------+
            var previewStyle = new PreviewStyle(
                canvasSize: new(
                    width: 716,
                    height: 204
                ),
                canvasStyle: new(
                    marginStyle: MarginStyle.Empty,
                    borderStyle: new(
                        color: SystemColors.Highlight,
                        all: 5,
                        depth: 3),
                    paddingStyle: new(
                        all: 1),
                    backgroundStyle: new(
                        color1: Color.FromArgb(13, 87, 210), // light blue
                        color2: Color.FromArgb(3, 68, 192) // darker blue
                    )
                ),
                screenStyle: new(
                    marginStyle: new(
                        all: 1),
                    borderStyle: new(
                        color: SystemColors.Highlight,
                        all: 5,
                        depth: 3),
                    paddingStyle: PaddingStyle.Empty,
                    backgroundStyle: new(
                        color1: Color.FromArgb(13, 87, 210), // light blue
                        color2: Color.FromArgb(3, 68, 192) // darker blue
                    )),
                extraColors: Array.Empty<Color>());
            var displayInfo = new DisplayInfo(
                devices: new List<DeviceInfo>
                {
                    new(
                        hostname: "localhost",
                        localhost: true,
                        screens: new List<ScreenInfo>
                        {
                            new(
                                handle: 0,
                                primary: true,
                                displayArea: new(-1920, -480, 1920, 1080),
                                workingArea: new(-1920, -480, 1920, 1080)),
                            new(
                                handle: 0,
                                primary: false,
                                displayArea: new(0, 0, 5120, 1440),
                                workingArea: new(0, 0, 5120, 1440)),
                        }
                    ),
                });
            var activatedScreen = displayInfo.Devices[0].Screens[0];
            var activatedLocation = activatedScreen.DisplayArea.Midpoint;

            var timer = Stopwatch.StartNew();
            for (var i = 0; i < 10_000; i++)
            {
                var formLayout = LayoutHelper.GetFormLayout(previewStyle, displayInfo, activatedScreen, activatedLocation);
            }

            timer.Stop();

            // runs on my machine in about 180-200ms, so leave a bit of headroom
            Assert.IsTrue(timer.ElapsedMilliseconds < 225);
        }
    }
}
