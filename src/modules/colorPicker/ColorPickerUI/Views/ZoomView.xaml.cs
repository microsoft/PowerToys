// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Numerics;

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;
using Windows.UI;

using DrawingColor = System.Drawing.Color;

namespace ColorPicker.Views
{
    /// <summary>
    /// The zoom magnifier surface. A single Win2D <see cref="CanvasControl"/> draws the captured
    /// screen region scaled with nearest-neighbor filtering, then overlays a brightness-adaptive
    /// pixel grid + pointer-pixel highlight -- the WinUI 3 replacement for the WPF GridShaderEffect.
    /// </summary>
    public sealed partial class ZoomView : UserControl
    {
        private const int BaseZoomImageSize = 50;
        private const double CursorSamplePatchSize = 4;
        internal const int ResizeDurationMilliseconds = 200;

        private CanvasBitmap _zoomBitmap;
        private Color[] _zoomPixels;

        private Storyboard _resizeStoryboard;
        private Vector2 _pointerOffsetFromHostCenter;

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
            StopResizeAnimation();
            ApplyZoom(bitmap, zoomFactor);
        }

        private void ApplyZoom(CanvasBitmap bitmap, double zoomFactor)
        {
            if (!ReferenceEquals(_zoomBitmap, bitmap))
            {
                // Cache the source pixels once per capture so the brightness-adaptive grid does not
                // read back from the GPU on every draw.
                _zoomBitmap = bitmap;
                _zoomPixels = bitmap?.GetPixelColors();
                _pointerOffsetFromHostCenter = Vector2.Zero;
            }

            var size = BaseZoomImageSize * zoomFactor;
            ZoomCanvas.Width = size;
            ZoomCanvas.Height = size;
            ZoomCanvas.Invalidate();
        }

        /// <summary>
        /// Animates only the captured image from its current rendered size to the requested zoom
        /// size. This mirrors the WPF <c>ResizeBehavior</c>: an in-flight animation is handed off
        /// from its presentation value, the border chrome stays fixed, grow uses Sine EaseOut,
        /// shrink uses Quadratic EaseIn, and both directions take 200ms.
        /// </summary>
        public void AnimateResize(CanvasBitmap bitmap, double currentZoomFactor)
        {
            // Read the layout size before stopping the old storyboard. Its base Width/Height already
            // contain the prior target, while ActualWidth/ActualHeight are the values on screen now.
            UpdateLayout();
            double currentWidth = GetRenderedDimension(ZoomCanvas.ActualWidth, ZoomCanvas.Width);
            double currentHeight = GetRenderedDimension(ZoomCanvas.ActualHeight, ZoomCanvas.Height);

            StopResizeAnimation();
            ApplyZoom(bitmap, currentZoomFactor);

            ResizeAnimationPlan widthPlan = GetResizeAnimationPlan(currentWidth, currentZoomFactor);
            ResizeAnimationPlan heightPlan = GetResizeAnimationPlan(currentHeight, currentZoomFactor);
            if (!widthPlan.ShouldAnimate && !heightPlan.ShouldAnimate)
            {
                return;
            }

            var storyboard = new Storyboard();
            if (widthPlan.ShouldAnimate)
            {
                var widthAnimation = CreateDimensionAnimation(widthPlan);
                Storyboard.SetTarget(widthAnimation, ZoomCanvas);
                Storyboard.SetTargetProperty(widthAnimation, "Width");
                storyboard.Children.Add(widthAnimation);
            }

            if (heightPlan.ShouldAnimate)
            {
                var heightAnimation = CreateDimensionAnimation(heightPlan);
                Storyboard.SetTarget(heightAnimation, ZoomCanvas);
                Storyboard.SetTargetProperty(heightAnimation, "Height");
                storyboard.Children.Add(heightAnimation);
            }

            storyboard.Completed += (_, _) =>
            {
                if (ReferenceEquals(_resizeStoryboard, storyboard))
                {
                    _resizeStoryboard = null;
                    ZoomCanvas.Invalidate();
                }
            };

            _resizeStoryboard = storyboard;
            storyboard.Begin();
        }

        /// <summary>
        /// Drops the cached captured bitmap so its owner (<see cref="Helpers.ZoomWindowHelper"/>) can
        /// dispose the Win2D surface without the canvas drawing a released bitmap. Called while the
        /// magnifier window is hidden (between zoom sessions), so no redraw is in flight.
        /// </summary>
        public void ClearBitmap()
        {
            StopResizeAnimation();
            _zoomBitmap = null;
            _zoomPixels = null;
            _pointerOffsetFromHostCenter = Vector2.Zero;
        }

        /// <summary>Cancels any in-flight resize and snaps the image to its requested size.</summary>
        public void ResetScale()
        {
            StopResizeAnimation();
            ZoomCanvas.Invalidate();
        }

        internal bool UpdatePointerPosition(
            Point pointerPosition,
            Point windowCenterPosition,
            double fallbackRasterizationScale,
            out DrawingColor color)
        {
            double actualRasterizationScale = XamlRoot?.RasterizationScale ?? 0;
            double rasterizationScale = actualRasterizationScale > 0
                ? actualRasterizationScale
                : fallbackRasterizationScale;
            _pointerOffsetFromHostCenter = GetPointerOffsetFromScreenPosition(
                pointerPosition,
                windowCenterPosition,
                rasterizationScale);
            ZoomCanvas.Invalidate();
            return TryGetPointerColor(out color);
        }

        internal bool TryGetPointerColor(out DrawingColor color)
        {
            color = DrawingColor.Transparent;
            double canvasWidth = GetRenderedDimension(ZoomCanvas.ActualWidth, ZoomCanvas.Width);
            double canvasHeight = GetRenderedDimension(ZoomCanvas.ActualHeight, ZoomCanvas.Height);
            if (_zoomPixels == null ||
                !TryGetPointerSample(
                    canvasWidth,
                    canvasHeight,
                    _pointerOffsetFromHostCenter,
                    BaseZoomImageSize,
                    out _,
                    out int pixelX,
                    out int pixelY))
            {
                return false;
            }

            Color pixel = _zoomPixels[(pixelY * BaseZoomImageSize) + pixelX];
            color = DrawingColor.FromArgb(pixel.A, pixel.R, pixel.G, pixel.B);
            return true;
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

            // Brightness-adaptive pixel grid + pointer highlight, only at high zoom (matches the
            // original shader's zoomFactor >= 4 gate). Each grid segment is drawn dark over a
            // light cell and light over a dark cell (a flat gray grid loses contrast on both very
            // light and very dark regions), and faded toward the magnifier edge so the cursor area
            // reads clearest (the shader's radius reveal). The pointer pixel gets an adaptive
            // (dark-or-light) highlight so it stays visible even on a white pixel.
            if (_zoomPixels == null || !ShouldDrawPixelGrid(w, h))
            {
                return;
            }

            const int n = BaseZoomImageSize;
            float cell = w / n;
            if (!TryGetPointerSample(
                    w,
                    h,
                    _pointerOffsetFromHostCenter,
                    n,
                    out Vector2 pointerPosition,
                    out int pointerPixelX,
                    out int pointerPixelY))
            {
                return;
            }

            float maxDist = Math.Max(
                Math.Max(pointerPosition.Length(), Vector2.Distance(pointerPosition, new Vector2(w, 0))),
                Math.Max(Vector2.Distance(pointerPosition, new Vector2(0, h)), Vector2.Distance(pointerPosition, new Vector2(w, h))));

            for (int j = 0; j < n; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    float midDist = Vector2.Distance(new Vector2((i + 0.5f) * cell, (j + 0.5f) * cell), pointerPosition);
                    float fade = 1f - (midDist / maxDist);
                    if (fade <= 0.05f)
                    {
                        continue;
                    }

                    byte alpha = (byte)(Math.Clamp(fade, 0f, 1f) * 160f);
                    var line = IsLight(_zoomPixels[(j * n) + i]) ? Colors.Black : Colors.White;
                    var seg = Color.FromArgb(alpha, line.R, line.G, line.B);

                    float x = i * cell;
                    float y = j * cell;
                    ds.DrawLine(x, y, x, y + cell, seg, 1f); // left edge of the cell
                    ds.DrawLine(x, y, x + cell, y, seg, 1f); // top edge of the cell
                }
            }

            // Highlight the source cell under the live pointer, then restore a small opaque patch at
            // the exact pointer position. MouseInfoProvider samples that physical screen pixel, so
            // drawing the patch last prevents the grid/highlight from becoming the copied color.
            float cellX = pointerPixelX * cell;
            float cellY = pointerPixelY * cell;
            Color pointerPixel = _zoomPixels[(pointerPixelY * n) + pointerPixelX];
            var highlight = IsLight(pointerPixel) ? Colors.Black : Colors.White;
            ds.DrawRectangle(new Rect(cellX, cellY, cell, cell), highlight, 2f);

            Color cursorSample = Color.FromArgb(byte.MaxValue, pointerPixel.R, pointerPixel.G, pointerPixel.B);
            ds.FillRectangle(GetCursorSamplePatchBounds(pointerPosition), cursorSample);
        }

        internal static bool TryGetPointerSample(
            double canvasWidth,
            double canvasHeight,
            Vector2 pointerOffsetFromHostCenter,
            int bitmapSize,
            out Vector2 pointerPosition,
            out int pixelX,
            out int pixelY)
        {
            pointerPosition = new Vector2(
                (float)((canvasWidth * 0.5) + pointerOffsetFromHostCenter.X),
                (float)((canvasHeight * 0.5) + pointerOffsetFromHostCenter.Y));
            pixelX = -1;
            pixelY = -1;

            if (bitmapSize <= 0 || canvasWidth <= 0 || canvasHeight <= 0 ||
                pointerPosition.X < 0 || pointerPosition.X >= canvasWidth ||
                pointerPosition.Y < 0 || pointerPosition.Y >= canvasHeight)
            {
                return false;
            }

            pixelX = Math.Min((int)(pointerPosition.X / (canvasWidth / bitmapSize)), bitmapSize - 1);
            pixelY = Math.Min((int)(pointerPosition.Y / (canvasHeight / bitmapSize)), bitmapSize - 1);
            return true;
        }

        internal static Vector2 GetPointerOffsetFromScreenPosition(
            Point pointerPosition,
            Point windowCenterPosition,
            double rasterizationScale)
        {
            double scale = rasterizationScale > 0 ? rasterizationScale : 1.0;

            return new Vector2(
                (float)(((pointerPosition.X + 0.5) - windowCenterPosition.X) / scale),
                (float)(((pointerPosition.Y + 0.5) - windowCenterPosition.Y) / scale));
        }

        internal static Rect GetCursorSamplePatchBounds(Vector2 pointerPosition)
            => new(
                pointerPosition.X - (CursorSamplePatchSize / 2),
                pointerPosition.Y - (CursorSamplePatchSize / 2),
                CursorSamplePatchSize,
                CursorSamplePatchSize);

        internal static bool ShouldDrawPixelGrid(double canvasWidth, double canvasHeight)
            => Math.Min(canvasWidth, canvasHeight) / BaseZoomImageSize >= 4;

        internal static ResizeAnimationPlan GetResizeAnimationPlan(
            double currentRenderedSize,
            double currentZoomFactor)
        {
            double targetSize = BaseZoomImageSize * currentZoomFactor;
            bool shouldAnimate = currentRenderedSize > 0 &&
                                 targetSize > 0 &&
                                 Math.Abs(currentRenderedSize - targetSize) >= 0.001;

            return new ResizeAnimationPlan(
                currentRenderedSize,
                targetSize,
                shouldAnimate,
                currentRenderedSize < targetSize ? ResizeEasing.SineEaseOut : ResizeEasing.QuadraticEaseIn);
        }

        internal static double GetRenderedDimension(double actualSize, double requestedSize)
            => actualSize > 0 && !double.IsNaN(actualSize) && !double.IsInfinity(actualSize)
                ? actualSize
                : requestedSize;

        private static DoubleAnimation CreateDimensionAnimation(ResizeAnimationPlan plan)
            => new()
            {
                From = plan.From,
                To = plan.To,
                Duration = new Duration(TimeSpan.FromMilliseconds(ResizeDurationMilliseconds)),
                EasingFunction = plan.Easing == ResizeEasing.SineEaseOut
                    ? new SineEase { EasingMode = EasingMode.EaseOut }
                    : new QuadraticEase { EasingMode = EasingMode.EaseIn },
                EnableDependentAnimation = true,
                FillBehavior = FillBehavior.Stop,
            };

        private void StopResizeAnimation()
        {
            _resizeStoryboard?.Stop();
            _resizeStoryboard = null;
        }

        internal enum ResizeEasing
        {
            SineEaseOut,
            QuadraticEaseIn,
        }

        internal readonly record struct ResizeAnimationPlan(
            double From,
            double To,
            bool ShouldAnimate,
            ResizeEasing Easing);

        // Perceived luminance (Rec. 601) above ~55% — used to pick a contrasting grid/highlight color.
        private static bool IsLight(Color c) => ((0.299 * c.R) + (0.587 * c.G) + (0.114 * c.B)) > 140.0;
    }
}
