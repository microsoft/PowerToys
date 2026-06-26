// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.UI;

namespace ColorPicker.Views
{
    /// <summary>
    /// The zoom magnifier surface. A single Win2D <see cref="CanvasControl"/> draws the captured
    /// screen region scaled with nearest-neighbor filtering, then overlays the pixel grid -- the
    /// WinUI 3 replacement for the WPF GridShaderEffect (see GridShader.fx for the original spec).
    /// </summary>
    public sealed partial class ZoomView : UserControl
    {
        private const int BaseZoomImageSize = 50;

        private CanvasBitmap _zoomBitmap;
        private double _zoomFactor = 1;

        public ZoomView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Sets the captured region + zoom factor and repaints. The canvas is sized to
        /// <c>BaseZoomImageSize * zoomFactor</c> so each source pixel maps to a square cell.
        /// </summary>
        public void SetZoom(CanvasBitmap bitmap, double zoomFactor)
        {
            _zoomBitmap = bitmap;
            _zoomFactor = zoomFactor;

            var size = BaseZoomImageSize * zoomFactor;
            ZoomCanvas.Width = size;
            ZoomCanvas.Height = size;
            ZoomCanvas.Invalidate();
        }

        private void ZoomCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (_zoomBitmap == null)
            {
                return;
            }

            var ds = args.DrawingSession;
            float w = (float)sender.Size.Width;
            float h = (float)sender.Size.Height;

            // Draw the captured region scaled to fill the canvas; nearest-neighbor keeps pixels crisp.
            ds.DrawImage(
                _zoomBitmap,
                new Rect(0, 0, w, h),
                new Rect(0, 0, _zoomBitmap.SizeInPixels.Width, _zoomBitmap.SizeInPixels.Height),
                1f,
                CanvasImageInterpolation.NearestNeighbor);

            // Pixel grid + center highlight, only at high zoom (matches the shader's zoomFactor >= 4 gate).
            if (_zoomFactor < 4)
            {
                return;
            }

            float cell = w / BaseZoomImageSize;
            var gridColor = new Color { A = 120, R = 128, G = 128, B = 128 };
            for (int i = 1; i < BaseZoomImageSize; i++)
            {
                float p = i * cell;
                ds.DrawLine(p, 0, p, h, gridColor, 1f);
                ds.DrawLine(0, p, w, p, gridColor, 1f);
            }

            // The cursor sits at the centre of the captured region; highlight that pixel.
            int centerIndex = BaseZoomImageSize / 2;
            float c = centerIndex * cell;
            ds.DrawRectangle(new Rect(c, c, cell, cell), Colors.White, 2f);
        }
    }
}
