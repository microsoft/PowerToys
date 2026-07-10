// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Numerics;

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Windows.Foundation;
using Windows.UI;

namespace ColorPicker.Views
{
    /// <summary>
    /// The zoom magnifier surface. A single Win2D <see cref="CanvasControl"/> draws the captured
    /// screen region scaled with nearest-neighbor filtering, then overlays a brightness-adaptive
    /// pixel grid + center-pixel highlight -- the WinUI 3 replacement for the WPF GridShaderEffect.
    /// </summary>
    public sealed partial class ZoomView : UserControl
    {
        private const int BaseZoomImageSize = 50;

        // Card (Border) on-screen size = canvas (50*factor) + canvas Margin (3*2) + BorderThickness (1*2).
        private const double CardChrome = 8;
        private static readonly TimeSpan ResizeDuration = TimeSpan.FromMilliseconds(200);

        private CanvasBitmap _zoomBitmap;
        private Color[] _zoomPixels;
        private double _zoomFactor = 1;

        private Visual _cardVisual;
        private bool _centerPinned;

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

            // Cache the source pixels once per capture so the brightness-adaptive grid does not
            // read back from the GPU on every draw.
            _zoomPixels = bitmap?.GetPixelColors();
            _zoomFactor = zoomFactor;

            var size = BaseZoomImageSize * zoomFactor;
            ZoomCanvas.Width = size;
            ZoomCanvas.Height = size;
            ZoomCanvas.Invalidate();
        }

        /// <summary>
        /// Animates the card from its previous on-screen size to the current one (already set by the
        /// last <see cref="SetZoom"/>) by scaling its Composition <see cref="Visual"/> about its own
        /// centre, so the magnified centre pixel stays under the cursor. Port of the WPF
        /// <c>ResizeBehavior</c> feel: grow eases out (≈SineEase EaseOut), shrink eases in
        /// (≈QuadraticEase EaseIn), both 200ms. The host <see cref="ZoomWindow"/> is a fixed max size,
        /// so the briefly-larger card during a shrink never clips and no window resize is needed.
        /// </summary>
        public void AnimateResize(double previousZoomFactor, double currentZoomFactor)
        {
            // The new size is in the layout tree from SetZoom; force layout so the pinned CenterPoint
            // and the keyframes are computed against the correct (new) card size.
            UpdateLayout();

            _cardVisual ??= ElementCompositionPreview.GetElementVisual(ZoomCard);
            var compositor = _cardVisual.Compositor;

            // Pin the scale origin to the live centre of the card (its size changes per zoom step).
            if (!_centerPinned)
            {
                var center = compositor.CreateExpressionAnimation("Vector3(this.Target.Size.X * 0.5, this.Target.Size.Y * 0.5, 0)");
                _cardVisual.StartAnimation("CenterPoint", center);
                _centerPinned = true;
            }

            double oldSize = (BaseZoomImageSize * previousZoomFactor) + CardChrome;
            double newSize = (BaseZoomImageSize * currentZoomFactor) + CardChrome;
            float startScale = newSize > 0 ? (float)(oldSize / newSize) : 1f;

            // No size change (or first appearance): snap, no tween.
            if (Math.Abs(startScale - 1f) < 0.001f)
            {
                _cardVisual.StopAnimation("Scale");
                _cardVisual.Scale = Vector3.One;
                return;
            }

            bool growing = startScale < 1f;

            // Easing chosen to match the WPF ResizeBehavior: grow ≈ SineEase EaseOut, shrink ≈
            // QuadraticEase EaseIn (the standard cubic-bezier approximations; no Sine/Quad primitive
            // exists in Composition, and the difference is imperceptible over 200ms at ≤2x scale).
            CompositionEasingFunction easing = growing
                ? compositor.CreateCubicBezierEasingFunction(new Vector2(0.39f, 0.575f), new Vector2(0.565f, 1f))
                : compositor.CreateCubicBezierEasingFunction(new Vector2(0.55f, 0.085f), new Vector2(0.68f, 0.53f));

            var anim = compositor.CreateVector3KeyFrameAnimation();
            anim.InsertKeyFrame(0f, new Vector3(startScale, startScale, 1f));
            anim.InsertKeyFrame(1f, Vector3.One, easing);
            anim.Duration = ResizeDuration;

            // Starting a new Scale animation implicitly replaces any in-flight one (handoff) — a fast
            // scroll burst never compounds.
            _cardVisual.StopAnimation("Scale");
            _cardVisual.StartAnimation("Scale", anim);
        }

        /// <summary>
        /// Drops the cached captured bitmap so its owner (<see cref="Helpers.ZoomWindowHelper"/>) can
        /// dispose the Win2D surface without the canvas drawing a released bitmap. Called while the
        /// magnifier window is hidden (between zoom sessions), so no redraw is in flight.
        /// </summary>
        public void ClearBitmap()
        {
            _zoomBitmap = null;
            _zoomPixels = null;
        }

        /// <summary>Cancels any in-flight scale and snaps the card to its natural size.</summary>
        public void ResetScale()
        {
            if (_cardVisual != null)
            {
                _cardVisual.StopAnimation("Scale");
                _cardVisual.Scale = Vector3.One;
            }
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

            // Brightness-adaptive pixel grid + center highlight, only at high zoom (matches the
            // original shader's zoomFactor >= 4 gate). Each grid segment is drawn dark over a
            // light cell and light over a dark cell (a flat gray grid loses contrast on both very
            // light and very dark regions), and faded toward the magnifier edge so the cursor area
            // reads clearest (the shader's radius reveal). The center cursor pixel gets an adaptive
            // (dark-or-light) highlight so it stays visible even on a white pixel.
            if (_zoomFactor < 4 || _zoomPixels == null)
            {
                return;
            }

            const int n = BaseZoomImageSize;
            float cell = w / n;
            var center = new Vector2(w * 0.5f, h * 0.5f);
            float maxDist = center.Length();

            for (int j = 0; j < n; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    float midDist = Vector2.Distance(new Vector2((i + 0.5f) * cell, (j + 0.5f) * cell), center);
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

            // The cursor sits at the centre of the captured region; highlight that pixel with a
            // dark-or-light border chosen from its own brightness so it never disappears.
            int centerIndex = n / 2;
            float cc = centerIndex * cell;
            var highlight = IsLight(_zoomPixels[(centerIndex * n) + centerIndex]) ? Colors.Black : Colors.White;
            ds.DrawRectangle(new Rect(cc, cc, cell, cell), highlight, 2f);
        }

        // Perceived luminance (Rec. 601) above ~55% — used to pick a contrasting grid/highlight color.
        private static bool IsLight(Color c) => ((0.299 * c.R) + (0.587 * c.G) + (0.114 * c.B)) > 140.0;
    }
}
