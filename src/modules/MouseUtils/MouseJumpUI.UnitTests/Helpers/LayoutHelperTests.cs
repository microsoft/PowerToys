// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseJumpUI.Helpers.Tests;

[TestClass]
public static class LayoutHelperTests
{
    [TestClass]
    public class CenterObjectTests
    {
        public class TestCase
        {
            public TestCase(Size obj, Point midpoint, Point expectedResult)
            {
                this.Obj = obj;
                this.Midpoint = midpoint;
                this.ExpectedResult = expectedResult;
            }

            public Size Obj { get; set; }

            public Point Midpoint { get; set; }

            public Point ExpectedResult { get; set; }
        }

        public static IEnumerable<object[]> GetTestCases()
        {
            // zero-sized object should center exactly on the midpoint
            yield return new[] { new TestCase(new(0, 0), new(0, 0), new(0, 0)), };

            // odd-sized objects should center above/left of the midpoint
            yield return new[] { new TestCase(new(1, 1), new(1, 1), new(0, 0)), };
            yield return new[] { new TestCase(new(1, 1), new(5, 5), new(4, 4)), };

            // even-sized objects should center exactly on the midpoint
            yield return new[] { new TestCase(new(2, 2), new(1, 1), new(0, 0)), };
            yield return new[] { new TestCase(new(2, 2), new(5, 5), new(4, 4)), };
            yield return new[] { new TestCase(new(800, 600), new(1000, 1000), new(600, 700)), };

            // negative result position
            yield return new[] { new TestCase(new(1000, 1200), new(300, 300), new(-200, -300)), };
        }

        [TestMethod]
        [DynamicData(nameof(GetTestCases), DynamicDataSourceType.Method)]
        public void RunTestCases(TestCase data)
        {
            var actual = LayoutHelper.CenterObject(data.Obj, data.Midpoint);
            var expected = data.ExpectedResult;
            Assert.AreEqual(expected, actual);
        }
    }

    [TestClass]
    public class CombineRegionsTests
    {
        public class TestCase
        {
            public TestCase(List<Rectangle> bounds, Rectangle expectedResult)
            {
                this.Bounds = bounds;
                this.ExpectedResult = expectedResult;
            }

            public List<Rectangle> Bounds { get; set; }

            public Rectangle ExpectedResult { get; set; }
        }

        public static IEnumerable<object[]> GetTestCases()
        {
            // empty list
            yield return new[]
            {
                new TestCase(
                    new(),
                    Rectangle.Empty),
            };

            // empty bounds
            yield return new[]
            {
                new TestCase(
                    new()
                    {
                        Rectangle.Empty,
                    },
                    Rectangle.Empty),
            };

            // single region
            //
            // +---+
            // | 0 |
            // +---+
            yield return new[]
            {
                new TestCase(
                    new()
                    {
                        new(100, 100, 100, 100),
                    },
                    new(100, 100, 100, 100)),
            };

            // multi-monitor desktop
            //
            // +----------------+
            // |                |
            // |       1        +-------+
            // |                |   0   |
            // +----------------+-------+
            yield return new[]
            {
                new TestCase(
                    new()
                    {
                        new(5120, 0, 1920, 1080),
                        new(0, 0, 5120, 1440),
                    },
                    new(0, 0, 7040, 1440)),
            };

            // multi-monitor desktop
            //
            // note - windows puts the *primary* monitor at the origin (0,0),
            // so screens positioned *above* or *left* will have negative coordinates
            //
            // +-------+
            // |   0   |
            // +-------+--------+
            // |                |
            // |       1        |
            // |                |
            // +----------------+
            yield return new[]
            {
                new TestCase(
                    new()
                    {
                        new(0, -1000, 1920, 1080),
                        new(0, 0, 5120, 1440),
                    },
                    new(0, -1000, 5120, 2440)),
            };

            // multi-monitor desktop
            //
            // note - windows puts the *primary* monitor at the origin (0,0),
            // so screens positioned *above* or *left* will have negative coordinates
            //
            // +-------+----------------+
            // |   0   |                |
            // +-------+       1        |
            //         |                |
            //         +----------------+
            yield return new[]
            {
                new TestCase(
                    new()
                    {
                        new(-1920, 0, 1920, 1080),
                        new(0, 0, 5120, 1440),
                    },
                    new(-1920, 0, 7040, 1440)),
            };

            // non-contiguous regions
            //
            // +---+
            // | 0 |    +-------+
            // +---+    |       |
            //          |   1   |
            //          |       |
            //          +-------+
            yield return new[]
            {
                new TestCase(
                    new()
                    {
                        new(0, 0, 100, 100),
                        new(200, 150, 200, 200),
                    },
                    new(0, 0, 400, 350)),
            };
        }

        [TestMethod]
        [DynamicData(nameof(GetTestCases), DynamicDataSourceType.Method)]
        public void RunTestCases(TestCase data)
        {
            var actual = LayoutHelper.CombineRegions(data.Bounds);
            var expected = data.ExpectedResult;
            Assert.AreEqual(expected, actual);
        }
    }

    [TestClass]
    public class GetMidpointTests
    {
    }

    [TestClass]
    public class MoveInsideTests
    {
        public class TestCase
        {
            public TestCase(Rectangle obj, Rectangle bounds, Rectangle expectedResult)
            {
                this.Obj = obj;
                this.Bounds = bounds;
                this.ExpectedResult = expectedResult;
            }

            public Rectangle Obj { get; set; }

            public Rectangle Bounds { get; set; }

            public Rectangle ExpectedResult { get; set; }
        }

        public static IEnumerable<object[]> GetTestCases()
        {
            // already inside - obj fills bounds exactly
            yield return new[]
            {
                new TestCase(new(0, 0, 100, 100), new(0, 0, 100, 100), new(0, 0, 100, 100)),
            };

            // already inside - obj exactly in each corner
            yield return new[]
            {
                new TestCase(new(0, 0, 100, 100), new(0, 0, 200, 200), new(0, 0, 100, 100)),
            };
            yield return new[]
            {
                new TestCase(new(100, 0, 100, 100), new(0, 0, 200, 200), new(100, 0, 100, 100)),
            };
            yield return new[]
            {
                new TestCase(new(0, 100, 100, 100), new(0, 0, 200, 200), new(0, 100, 100, 100)),
            };
            yield return new[]
            {
                new TestCase(new(100, 100, 100, 100), new(0, 0, 200, 200), new(100, 100, 100, 100)),
            };

            // move inside - obj outside each corner
            yield return new[]
            {
                new TestCase(new(-50, -50, 100, 100), new(0, 0, 200, 200), new(0, 0, 100, 100)),
            };
            yield return new[]
            {
                new TestCase(new(250, -50, 100, 100), new(0, 0, 200, 200), new(100, 0, 100, 100)),
            };
            yield return new[]
            {
                new TestCase(new(-50, 250, 100, 100), new(0, 0, 200, 200), new(0, 100, 100, 100)),
            };
            yield return new[]
            {
                new TestCase(new(150, 150, 100, 100), new(0, 0, 200, 200), new(100, 100, 100, 100)),
            };
        }

        [TestMethod]
        [DynamicData(nameof(GetTestCases), DynamicDataSourceType.Method)]
        public void RunTestCases(TestCase data)
        {
            var actual = LayoutHelper.MoveInside(data.Obj, data.Bounds);
            var expected = data.ExpectedResult;
            Assert.AreEqual(expected, actual);
        }
    }

    [TestClass]
    public class ScaleLocationTests
    {
    }

    [TestClass]
    public class ScaleToFitTests
    {
        public class TestCase
        {
            public TestCase(Size obj, Size bounds, Size expectedResult)
            {
                this.Obj = obj;
                this.Bounds = bounds;
                this.ExpectedResult = expectedResult;
            }

            public Size Obj { get; set; }

            public Size Bounds { get; set; }

            public Size ExpectedResult { get; set; }
        }

        public static IEnumerable<object[]> GetTestCases()
        {
            // identity tests
            yield return new[]
            {
                new TestCase(new(0, 0), new(0, 0), new(0, 0)),
            };
            yield return new[]
            {
                new TestCase(new(512, 384), new(512, 384), new(512, 384)),
            };
            yield return new[]
            {
                new TestCase(new(1024, 768), new(1024, 768), new(1024, 768)),
            };

            // integer scaling factor tests
            yield return new[]
            {
                new TestCase(new(512, 384), new(2048, 1536), new(2048, 1536)),
            };
            yield return new[]
            {
                new TestCase(new(2048, 1536), new(1024, 768), new(1024, 768)),
            };

            // scale to fit width
            yield return new[]
            {
                new TestCase(new(512, 384), new(2048, 3072), new(2048, 1536)),
            };

            // scale to fit height
            yield return new[]
            {
                new TestCase(new(512, 384), new(4096, 1536), new(2048, 1536)),
            };
        }

        [TestMethod]
        [DynamicData(nameof(GetTestCases), DynamicDataSourceType.Method)]
        public void RunTestCases(TestCase data)
        {
            var actual = LayoutHelper.ScaleToFit(data.Obj, data.Bounds);
            var expected = data.ExpectedResult;
            Assert.AreEqual(expected, actual);
        }
    }

    [TestClass]
    public class GetPreviewFormBoundsTests
    {
        public class TestCase
        {
            public TestCase(
                Rectangle desktopBounds,
                Point cursorPosition,
                Rectangle currentMonitorBounds,
                Size maximumPreviewImageSize,
                Size previewImagePadding,
                Rectangle expectedResult)
            {
                this.DesktopBounds = desktopBounds;
                this.CursorPosition = cursorPosition;
                this.CurrentMonitorBounds = currentMonitorBounds;
                this.MaximumPreviewImageSize = maximumPreviewImageSize;
                this.PreviewImagePadding = previewImagePadding;
                this.ExpectedResult = expectedResult;
            }

            public Rectangle DesktopBounds { get; set; }

            public Point CursorPosition { get; set; }

            public Rectangle CurrentMonitorBounds { get; set; }

            public Size MaximumPreviewImageSize { get; set; }

            public Size PreviewImagePadding { get; set; }

            public Rectangle ExpectedResult { get; set; }
        }

        public static IEnumerable<object[]> GetTestCases()
        {
            // multi-monitor desktop
            //
            // +----------------+
            // |                |
            // |       1        +-------+
            // |                |   0   |
            // +----------------+-------+
            //
            // clicked near top left corner so that the
            // preview box overhangs the top and left
            //
            // +----------------+
            // | *              |
            // |       1        +-------+
            // |                |   0   |
            // +----------------+-------+
            //
            // form is centered on mouse cursor and then
            // nudged back into the top left corner
            //
            // +-----+----------+
            // | *   |          |
            // +-----+ 1        +-------+
            // |                |   0   |
            // +----------------+-------+
            yield return new[]
            {
                new TestCase(
                    desktopBounds: new(-5120, -359, 7040, 1440),
                    cursorPosition: new(-5020, -259),
                    currentMonitorBounds: new(-5120, -359, 5120, 1440),
                    maximumPreviewImageSize: new(1600, 1200),
                    previewImagePadding: new(10, 10),
                    expectedResult: new(-5120, -359, 1610, 337)),
            };

            // multi-monitor desktop
            //
            // +----------------+
            // |                |
            // |       1        +-------+
            // |                |   0   |
            // +----------------+-------+
            //
            // clicked in the center of the second monitor
            //
            // +----------------+
            // |                |
            // |       *        +-------+
            // |                |   0   |
            // +----------------+-------+
            //
            // form is centered on the mouse cursor
            //
            // +----------------+
            // |    +-----+     |
            // |    |  *  |     +-------+
            // |    +-----+     |   0   |
            // +----------------+-------+
            yield return new[]
            {
                new TestCase(
                    desktopBounds: new(-5120, -359, 7040, 1440),
                    cursorPosition: new(-2560, 361),
                    currentMonitorBounds: new(-5120, -359, 5120, 1440),
                    maximumPreviewImageSize: new(1600, 1200),
                    previewImagePadding: new(10, 10),
                    expectedResult: new(-3365, 192, 1610, 337)),
            };

            // multi-monitor desktop
            //
            // +----------------+
            // |                |
            // |       1        +-------+
            // |                |   0   |
            // +----------------+-------+
            //
            // clicked in the center of the monitor
            //
            // +----------------+
            // |                |
            // |       *        +-------+
            // |                |   0   |
            // +----------------+-------+
            //
            // max preview is larger than monitor,
            // form is scaled to monitor size, with
            // consideration for image padding
            //
            // *----------------*
            // |+--------------+|
            // ||      *       |+-------+
            // |+--------------+|   0   |
            // +----------------+-------+
            yield return new[]
            {
                new TestCase(
                    desktopBounds: new(-5120, -359, 7040, 1440),
                    cursorPosition: new(-2560, 361),
                    currentMonitorBounds: new(-5120, -359, 5120, 1440),
                    maximumPreviewImageSize: new(160000, 120000),
                    previewImagePadding: new(10, 10),
                    expectedResult: new(-5120, -166, 5120, 1055)),
            };
        }

        [TestMethod]
        [DynamicData(nameof(GetTestCases), DynamicDataSourceType.Method)]
        public void RunTestCases(TestCase data)
        {
            var actual = LayoutHelper.GetPreviewFormBounds(
                desktopBounds: data.DesktopBounds,
                activatedPosition: data.CursorPosition,
                activatedMonitorBounds: data.CurrentMonitorBounds,
                maximumThumbnailImageSize: data.MaximumPreviewImageSize,
                thumbnailImagePadding: data.PreviewImagePadding);
            var expected = data.ExpectedResult;
            Assert.AreEqual(expected, actual);
        }
    }
}
