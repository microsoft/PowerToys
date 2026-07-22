// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;

using ColorPicker.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

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
        [DataRow(1.0, 215.0, 0.5)]
        [DataRow(1.5, 214.6666666667, 0.0)]
        [DataRow(2.0, 215.0, 0.25)]
        public void Pointer_offset_uses_the_physical_sample_pixel_center(
            double rasterizationScale,
            double pointerPosition,
            double expectedOffset)
        {
            Vector2 offset = ZoomView.GetPointerOffsetFromHostCenter(
                new Point(pointerPosition, pointerPosition),
                new Size(430, 430),
                rasterizationScale);

            Assert.AreEqual(expectedOffset, offset.X, 0.001);
            Assert.AreEqual(expectedOffset, offset.Y, 0.001);
        }
    }
}
