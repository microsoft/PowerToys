// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;

using ColorPicker.Mouse;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ColorPicker.UnitTests.Mouse
{
    /// <summary>
    /// Unit tests for <see cref="MouseInfoProvider.TryGetPixelColor"/>.
    /// These tests exercise the delegate-testable capture helper directly — no mock framework,
    /// no UI construction required.
    /// </summary>
    [TestClass]
    public class MouseInfoProviderTests
    {
        [TestMethod]
        public void TryGetPixelColor_success_returns_true_and_exact_color()
        {
            var expected = Color.FromArgb(12, 34, 56, 78);

            bool ok = MouseInfoProvider.TryGetPixelColor(() => expected, out Color actual);

            Assert.IsTrue(ok);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void TryGetPixelColor_Win32Exception_returns_false_and_transparent()
        {
            // Win32Exception is thrown by Graphics.CopyFromScreen when the desktop
            // context is unavailable (error "the handle is invalid").
            bool ok = MouseInfoProvider.TryGetPixelColor(
                () => throw new Win32Exception(0, "simulated GDI failure"),
                out Color actual);

            Assert.IsFalse(ok);
            Assert.AreEqual(Color.Transparent, actual);
        }

        [TestMethod]
        public void TryGetPixelColor_ExternalException_returns_false_and_transparent()
        {
            // Use a private concrete subclass so CA2201 is satisfied while still exercising
            // the ExternalException catch branch (Win32Exception would hit the earlier branch).
            bool ok = MouseInfoProvider.TryGetPixelColor(
                () => throw new GdiPlusStatusException("simulated external failure"),
                out Color actual);

            Assert.IsFalse(ok);
            Assert.AreEqual(Color.Transparent, actual);
        }

        // Private ExternalException subclass used to exercise the ExternalException catch branch
        // without triggering CA2201 (which rejects throwing the reserved base type directly).
        private sealed class GdiPlusStatusException : ExternalException
        {
            public GdiPlusStatusException(string message)
                : base(message)
            {
            }
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TryGetPixelColor_unexpected_exception_propagates()
        {
            _ = MouseInfoProvider.TryGetPixelColor(
                () => throw new InvalidOperationException("unexpected – must not be caught"),
                out _);
        }

        [TestMethod]
        public void GetSampleArea_is_centered_on_cursor()
        {
            var area = MouseInfoProvider.GetSampleArea(100, 200, 5, new Rectangle(0, 0, 1920, 1080));

            Assert.AreEqual(new Rectangle(98, 198, 5, 5), area);
        }

        [TestMethod]
        public void GetSampleArea_is_clipped_to_monitor_edge()
        {
            var area = MouseInfoProvider.GetSampleArea(0, 1079, 5, new Rectangle(0, 0, 1920, 1080));

            Assert.AreEqual(new Rectangle(0, 1077, 3, 3), area);
        }

        [TestMethod]
        public void GetSampleArea_is_clipped_to_monitor_with_negative_origin()
        {
            // Secondary monitor to the left of the primary one.
            var area = MouseInfoProvider.GetSampleArea(-1920, 10, 9, new Rectangle(-1920, 0, 1920, 1080));

            Assert.AreEqual(new Rectangle(-1920, 6, 5, 9), area);
        }

        [TestMethod]
        public void GetSampleArea_is_not_clipped_when_monitor_bounds_are_unknown()
        {
            var area = MouseInfoProvider.GetSampleArea(0, 0, 3, Rectangle.Empty);

            Assert.AreEqual(new Rectangle(-1, -1, 3, 3), area);
        }

        [TestMethod]
        public void GetAverageColor_of_uniform_area_returns_that_color()
        {
            using var bitmap = new Bitmap(3, 3);
            for (var y = 0; y < 3; y++)
            {
                for (var x = 0; x < 3; x++)
                {
                    bitmap.SetPixel(x, y, Color.FromArgb(10, 20, 30));
                }
            }

            Assert.AreEqual(Color.FromArgb(10, 20, 30), MouseInfoProvider.GetAverageColor(bitmap, 3, 3));
        }

        [TestMethod]
        public void GetAverageColor_averages_and_rounds_each_channel()
        {
            using var bitmap = new Bitmap(2, 1);
            bitmap.SetPixel(0, 0, Color.FromArgb(0, 0, 255));
            bitmap.SetPixel(1, 0, Color.FromArgb(255, 1, 0));

            // (0 + 255) / 2 = 127.5 and (0 + 1) / 2 = 0.5 both round up.
            Assert.AreEqual(Color.FromArgb(128, 1, 128), MouseInfoProvider.GetAverageColor(bitmap, 2, 1));
        }

        [TestMethod]
        public void GetAverageColor_only_reads_the_requested_area()
        {
            using var bitmap = new Bitmap(MouseInfoProvider.MaxSampleSize, MouseInfoProvider.MaxSampleSize);
            bitmap.SetPixel(0, 0, Color.FromArgb(200, 100, 50));
            bitmap.SetPixel(1, 0, Color.White);

            Assert.AreEqual(Color.FromArgb(200, 100, 50), MouseInfoProvider.GetAverageColor(bitmap, 1, 1));
        }

        [TestMethod]
        public void GetAverageColor_averages_the_largest_sample_area()
        {
            // Left half black, right half white, plus one extra white column: 13 of 25 columns are white.
            using var bitmap = new Bitmap(MouseInfoProvider.MaxSampleSize, MouseInfoProvider.MaxSampleSize);
            for (var y = 0; y < 25; y++)
            {
                for (var x = 0; x < 25; x++)
                {
                    bitmap.SetPixel(x, y, x < 12 ? Color.Black : Color.White);
                }
            }

            // 13 * 25 * 255 / 625 = 132.6, rounded to 133.
            Assert.AreEqual(Color.FromArgb(133, 133, 133), MouseInfoProvider.GetAverageColor(bitmap, 25, 25));
        }
    }
}
