// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CropAndLock.UITests
{
    internal sealed class CropImage
    {
        private const int BorderInset = 4;
        private const int ChannelTolerance = 30;
        private const int ContentThreshold = 40;

        private readonly byte[] pixels;
        private readonly int stride;
        private int background = -1;

        internal CropImage(Bitmap bitmap)
        {
            Size = bitmap.Size;
            var data = bitmap.LockBits(new Rectangle(Point.Empty, Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                stride = data.Stride;
                pixels = new byte[stride * Size.Height];
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        internal Size Size { get; }

        internal readonly record struct Comparison(double AllPixels, double ContentPixels, int ContentCount, Size ExpectedSize, Size ActualSize)
        {
            // Validated on Win10 19045 and Win11 26200, including 1-vCPU runs. Keep these fixed
            // when changing capture: background agreement alone must never admit a blank image.
            internal bool Matches => ExpectedSize == ActualSize && AllPixels >= 0.98 && ContentPixels >= 0.90 && ContentCount >= 100;
        }

        internal static CropImage Capture(IntPtr window, Rectangle clientRegion, string path)
        {
            var client = NativeMethods.ClientBounds(window);
            var region = clientRegion;
            region.Offset(client.Location);
            var (left, top, right, bottom) = WindowHelper.GetVisibleBounds(window);
            var frame = Rectangle.FromLTRB(left, top, right, bottom);
            Assert.IsTrue(frame.Contains(region), $"Capture region {region} is outside the DWM frame {frame}.");

            // The shared helper captures composed desktop pixels, not PrintWindow/UIA. Both UWP
            // content and DWM thumbnails would otherwise be absent from an apparently valid image.
            var windowPath = Path.ChangeExtension(path, ".window.png");
            WindowHelper.CaptureVisibleWindow(window, windowPath);
            using var full = new Bitmap(windowPath);
            region.Offset(-frame.Left, -frame.Top);
            using var cropped = full.Clone(region, PixelFormat.Format32bppArgb);
            cropped.Save(path, ImageFormat.Png);
            return new CropImage(cropped);
        }

        internal static Comparison Compare(CropImage expected, CropImage actual)
        {
            if (expected.Size != actual.Size || expected.Size.Width <= BorderInset * 2 || expected.Size.Height <= BorderInset * 2)
            {
                return new Comparison(0, 0, 0, expected.Size, actual.Size);
            }

            // Weight the foreground separately: a blank input must not pass simply because
            // its background occupies more than 98% of the crop. The inset excludes the outer focus edge.
            var background = expected.GetBackground();
            var total = 0;
            var matched = 0;
            var content = 0;
            var contentMatched = 0;
            for (var y = BorderInset; y < expected.Size.Height - BorderInset; y++)
            {
                for (var x = BorderInset; x < expected.Size.Width - BorderInset; x++)
                {
                    var expectedIndex = (y * expected.stride) + (x * 4);
                    var actualIndex = (y * actual.stride) + (x * 4);
                    var matches = true;
                    var isContent = false;
                    for (var channel = 0; channel < 3; channel++)
                    {
                        matches &= Math.Abs(expected.pixels[expectedIndex + channel] - actual.pixels[actualIndex + channel]) <= ChannelTolerance;
                        var backgroundChannel = ((background >> (channel * 4)) & 15) * 16;

                        // Require more contrast than the permitted channel drift and histogram quantization.
                        isContent |= Math.Abs(expected.pixels[expectedIndex + channel] - backgroundChannel) > ContentThreshold;
                    }

                    total++;
                    matched += matches ? 1 : 0;
                    content += isContent ? 1 : 0;
                    contentMatched += matches && isContent ? 1 : 0;
                }
            }

            return new Comparison((double)matched / total, content == 0 ? 0 : (double)contentMatched / content, content, expected.Size, actual.Size);
        }

        private int GetBackground()
        {
            if (background >= 0)
            {
                return background;
            }

            var colors = new Dictionary<int, int>();
            for (var y = BorderInset; y < Size.Height - BorderInset; y++)
            {
                for (var x = BorderInset; x < Size.Width - BorderInset; x++)
                {
                    var index = (y * stride) + (x * 4);
                    var color = (pixels[index] >> 4) | ((pixels[index + 1] >> 4) << 4) | ((pixels[index + 2] >> 4) << 8);
                    colors[color] = colors.GetValueOrDefault(color) + 1;
                }
            }

            background = colors.MaxBy(entry => entry.Value).Key;
            return background;
        }
    }
}
