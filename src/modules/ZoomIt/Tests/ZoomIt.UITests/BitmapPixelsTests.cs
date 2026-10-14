// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.ZoomIt.UITests;

[TestClass]
[TestCategory("ZoomIt")]
[DoNotParallelize]
public sealed class BitmapPixelsTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    [DataRow(PixelFormat.Format24bppRgb)]
    [DataRow(PixelFormat.Format32bppArgb)]
    [DataRow(PixelFormat.Format32bppPArgb)]
    public void BufferedPixelsMatchGetPixel(PixelFormat format)
    {
        using var bitmap = new Bitmap(17, 13, format);
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                bitmap.SetPixel(x, y, Color.FromArgb(128 + y, x * 13, y * 17, (x + y) * 7));
            }
        }

        var area = new Rectangle(3, 2, 11, 7);
        using var pixels = new BitmapPixels(bitmap, area);
        for (var y = 0; y < area.Height; y++)
        {
            for (var x = 0; x < area.Width; x++)
            {
                Assert.AreEqual(bitmap.GetPixel(area.X + x, area.Y + y).ToArgb(), pixels.GetColor(x, y).ToArgb(), $"Pixel ({x},{y}) in {format}.");
            }
        }
    }

    [TestMethod]
    public void BufferedPixelsHandleNegativeStride()
    {
        const int width = 7;
        const int height = 5;
        const int stride = width * 4;
        var memory = Marshal.AllocHGlobal(stride * height);
        try
        {
            var bytes = Enumerable.Range(0, stride * height).Select(index => (byte)(index % 256)).ToArray();
            Marshal.Copy(bytes, 0, memory, bytes.Length);
            using var bitmap = new Bitmap(width, height, -stride, PixelFormat.Format32bppArgb, IntPtr.Add(memory, stride * (height - 1)));
            using var pixels = new BitmapPixels(bitmap);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    Assert.AreEqual(bitmap.GetPixel(x, y).ToArgb(), pixels.GetColor(x, y).ToArgb(), $"Negative-stride pixel ({x},{y}).");
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }

    [TestMethod]
    public void ColorBoundsPreservesSamplingAndClipping()
    {
        using var bitmap = new Bitmap(64, 48);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Black);
            graphics.FillRectangle(Brushes.Lime, new Rectangle(13, 9, 20, 18));
        }

        Rectangle[] regions = [new(-5, -3, 20, 15), new(5, 7, 35, 21), new(0, 0, 64, 48), new(70, 50, 1, 1), new(5, 5, 0, 0)];
        foreach (var region in regions)
        {
            Assert.AreEqual(ReferenceBounds(bitmap, region), DesktopFixture.ColorBounds(bitmap, IsGreen, region), $"Region {region}.");
        }
    }

    [TestMethod]
    public void FullScreenColorBoundsMatchesReference()
    {
        using var bitmap = new Bitmap(1920, 1080);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(DesktopFixture.BackgroundColor);
            graphics.FillRectangle(Brushes.Lime, new Rectangle(920, 500, 80, 80));
        }

        var area = new Rectangle(Point.Empty, bitmap.Size);
        DesktopFixture.ColorBounds(bitmap, IsGreen);
        var timer = Stopwatch.StartNew();
        var expected = ReferenceBounds(bitmap, area);
        var referenceTime = timer.Elapsed;
        timer.Restart();
        var actual = DesktopFixture.ColorBounds(bitmap, IsGreen);
        var bufferedTime = timer.Elapsed;
        Assert.AreEqual(expected, actual);
        TestContext.WriteLine($"1920x1080 stride-2 scan: GetPixel={referenceTime.TotalMilliseconds:F2}ms; buffered={bufferedTime.TotalMilliseconds:F2}ms.");
    }

    private static bool IsGreen(Color color) => color.G > 200 && color.R < 30 && color.B < 30;

    private static Rectangle ReferenceBounds(Bitmap bitmap, Rectangle region)
    {
        var area = Rectangle.Intersect(region, new Rectangle(Point.Empty, bitmap.Size));
        var left = bitmap.Width;
        var top = bitmap.Height;
        var right = -1;
        var bottom = -1;
        for (var y = area.Top; y < area.Bottom; y += 2)
        {
            for (var x = area.Left; x < area.Right; x += 2)
            {
                if (IsGreen(bitmap.GetPixel(x, y)))
                {
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
            }
        }

        return right < left ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }
}
