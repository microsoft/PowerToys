// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;

using ColorPicker.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;
using Windows.UI;

namespace ColorPicker.UnitTests.Views
{
    [TestClass]
    public class ZoomViewTests
    {
        [TestMethod]
        public void Cursor_sample_patch_is_centered_on_the_live_pointer()
        {
            var bounds = ZoomView.GetCursorSamplePatchBounds(new Vector2(100, 80));

            Assert.AreEqual(98.0, bounds.X, 0.001);
            Assert.AreEqual(78.0, bounds.Y, 0.001);
            Assert.AreEqual(4.0, bounds.Width, 0.001);
            Assert.AreEqual(4.0, bounds.Height, 0.001);
        }

        [DataTestMethod]
        [DataRow(200.0, 3.0, 25)]
        [DataRow(200.0, 3.9, 25)]
        [DataRow(200.0, 4.0, 26)]
        [DataRow(400.0, 7.0, 25)]
        [DataRow(400.0, 8.0, 26)]
        public void Pointer_offset_maps_to_the_live_source_pixel(
            double canvasSize,
            double horizontalOffset,
            int expectedPixelX)
        {
            bool succeeded = ZoomView.TryGetPointerSample(
                canvasSize,
                canvasSize,
                new Vector2((float)horizontalOffset, 0),
                bitmapSize: 50,
                out Vector2 pointerPosition,
                out int pixelX,
                out int pixelY);

            Assert.IsTrue(succeeded);
            Assert.AreEqual(expectedPixelX, pixelX);
            Assert.AreEqual(25, pixelY);
            Assert.AreEqual((canvasSize * 0.5) + horizontalOffset, pointerPosition.X, 0.001);
        }

        [TestMethod]
        public void Pointer_outside_canvas_does_not_select_an_edge_pixel()
        {
            bool succeeded = ZoomView.TryGetPointerSample(
                canvasWidth: 200,
                canvasHeight: 200,
                pointerOffsetFromHostCenter: new Vector2(100, 0),
                bitmapSize: 50,
                out _,
                out int pixelX,
                out int pixelY);

            Assert.IsFalse(succeeded);
            Assert.AreEqual(-1, pixelX);
            Assert.AreEqual(-1, pixelY);
        }

        [DataTestMethod]
        [DataRow(1.0, 100.0, 100.0, 0.5)]
        [DataRow(1.5, 100.0, 100.5, 0.0)]
        [DataRow(1.25, 101.0, 100.0, 1.2)]
        [DataRow(2.0, 100.0, 100.0, 0.25)]
        [DataRow(0.0, 103.0, 100.0, 3.5)]
        public void Pointer_offset_uses_the_physical_pixel_and_window_centers(
            double rasterizationScale,
            double pointerPosition,
            double windowCenterPosition,
            double expectedOffset)
        {
            Vector2 offset = ZoomView.GetPointerOffsetFromScreenPosition(
                new Point(pointerPosition, pointerPosition),
                new Point(windowCenterPosition, windowCenterPosition),
                rasterizationScale);

            Assert.AreEqual(expectedOffset, offset.X, 0.001);
            Assert.AreEqual(expectedOffset, offset.Y, 0.001);
        }

        [TestMethod]
        public void Resize_animation_plan_uses_rendered_size_and_wpf_easing()
        {
            ZoomView.ResizeAnimationPlan growing = ZoomView.GetResizeAnimationPlan(75, currentZoomFactor: 2);
            Assert.AreEqual(75, growing.From, 0.001);
            Assert.AreEqual(100, growing.To, 0.001);
            Assert.IsTrue(growing.ShouldAnimate);
            Assert.AreEqual(ZoomView.ResizeEasing.SineEaseOut, growing.Easing);

            ZoomView.ResizeAnimationPlan shrinking = ZoomView.GetResizeAnimationPlan(250, currentZoomFactor: 4);
            Assert.AreEqual(250, shrinking.From, 0.001);
            Assert.AreEqual(200, shrinking.To, 0.001);
            Assert.IsTrue(shrinking.ShouldAnimate);
            Assert.AreEqual(ZoomView.ResizeEasing.QuadraticEaseIn, shrinking.Easing);

            // A quick logical zoom-out may still have to grow visually when the interrupted larger
            // step has not yet reached the new target. WPF selects easing from presentation values.
            ZoomView.ResizeAnimationPlan reversing = ZoomView.GetResizeAnimationPlan(150, currentZoomFactor: 4);
            Assert.AreEqual(150, reversing.From, 0.001);
            Assert.AreEqual(200, reversing.To, 0.001);
            Assert.AreEqual(ZoomView.ResizeEasing.SineEaseOut, reversing.Easing);

            Assert.IsFalse(ZoomView.GetResizeAnimationPlan(200, currentZoomFactor: 4).ShouldAnimate);
            Assert.IsFalse(ZoomView.GetResizeAnimationPlan(0, currentZoomFactor: 2).ShouldAnimate);
            Assert.AreEqual(200, ZoomView.ResizeDurationMilliseconds);
        }

        [DataTestMethod]
        [DataRow(100.0, false)]
        [DataRow(199.9, false)]
        [DataRow(200.0, true)]
        [DataRow(400.0, true)]
        public void Pixel_grid_waits_until_the_animated_cells_are_large_enough(
            double canvasSize,
            bool expected)
        {
            Assert.AreEqual(expected, ZoomView.ShouldDrawPixelGrid(canvasSize, canvasSize));
        }

        [TestMethod]
        public void Sample_cells_are_centered_on_the_pointer_pixel()
        {
            var sampleCells = ZoomView.GetSampleCells(bitmapSize: 50, pixelX: 25, pixelY: 25, sampleSize: 25);

            Assert.AreEqual(new System.Drawing.Rectangle(13, 13, 25, 25), sampleCells);
        }

        [TestMethod]
        public void Sample_cells_are_clipped_to_the_captured_region()
        {
            var sampleCells = ZoomView.GetSampleCells(bitmapSize: 50, pixelX: 1, pixelY: 48, sampleSize: 9);

            Assert.AreEqual(new System.Drawing.Rectangle(0, 44, 6, 6), sampleCells);
        }

        [TestMethod]
        public void Sample_color_of_a_single_pixel_is_the_pointer_pixel()
        {
            var pixels = new Color[3 * 3];
            pixels[4] = Color.FromArgb(200, 10, 20, 30);

            Assert.AreEqual(Color.FromArgb(200, 10, 20, 30), ZoomView.GetSampleColor(pixels, bitmapSize: 3, pixelX: 1, pixelY: 1, sampleSize: 1));
        }

        [TestMethod]
        public void Sample_color_averages_the_sampled_block()
        {
            // 3x3 capture: the center pixel is white and the rest are black, so a 3x3 average is 255 / 9 = 28.3.
            var pixels = new Color[3 * 3];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.FromArgb(255, 0, 0, 0);
            }

            pixels[4] = Color.FromArgb(255, 255, 255, 255);

            Assert.AreEqual(Color.FromArgb(255, 28, 28, 28), ZoomView.GetSampleColor(pixels, bitmapSize: 3, pixelX: 1, pixelY: 1, sampleSize: 3));
        }
    }
}
