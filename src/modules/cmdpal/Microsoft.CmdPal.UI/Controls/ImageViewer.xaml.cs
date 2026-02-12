// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.System;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class ImageViewer : UserControl
{
    public event EventHandler? CancelRequested;

    private const double MinScale = 0.25;
    private const double MaxScale = 8.0;
    private const double MinVisiblePadding = 24.0;
    private const double KeyboardPanStep = 24.0;

    private Grid? _host;

    private Point _lastPanPoint;
    private bool _isPanning;
    private double _scale = 1.0;

    public ImageViewer()
    {
        InitializeComponent();

        _host = Content as Grid;

        IsTabStop = true;
        KeyDown += OnKeyDown;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheelChanged;
        DoubleTapped += OnDoubleTapped;
        SizeChanged += OnSizeChanged;
    }

    public void Initialize(object? sourceKey)
    {
        ZoomImage.SourceKey = sourceKey;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ResetView();
        CenterImage();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ClampTranslation();
    }

    private void ResetView()
    {
        _scale = 1.0;
        ScaleTransform.ScaleX = _scale;
        ScaleTransform.ScaleY = _scale;
        TranslateTransform.X = 0.0;
        TranslateTransform.Y = 0.0;
        ClampTranslation();
    }

    private void CenterImage()
    {
        TranslateTransform.X = 0.0;
        TranslateTransform.Y = 0.0;
        ClampTranslation();
    }

    private void OnZoomInClick(object sender, RoutedEventArgs e)
    {
        ZoomRelative(1.1);
    }

    private void OnZoomOutClick(object sender, RoutedEventArgs e)
    {
        ZoomRelative(0.9);
    }

    private void OnZoomToFitClick(object sender, RoutedEventArgs e)
    {
        ResetView();
        CenterImage();
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Add:
                ZoomRelative(1.1);
                e.Handled = true;
                break;
            case VirtualKey.Subtract:
                ZoomRelative(0.9);
                e.Handled = true;
                break;
            case VirtualKey.Number0:
            case VirtualKey.NumberPad0:
                ResetView();
                CenterImage();
                e.Handled = true;
                break;
            case VirtualKey.R:
                CenterImage();
                e.Handled = true;
                break;
            case VirtualKey.Escape:
                CancelRequested?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                break;
            case VirtualKey.Left:
                TranslateTransform.X += KeyboardPanStep;
                ClampTranslation();
                e.Handled = true;
                break;
            case VirtualKey.Right:
                TranslateTransform.X -= KeyboardPanStep;
                ClampTranslation();
                e.Handled = true;
                break;
            case VirtualKey.Up:
                TranslateTransform.Y += KeyboardPanStep;
                ClampTranslation();
                e.Handled = true;
                break;
            case VirtualKey.Down:
                TranslateTransform.Y -= KeyboardPanStep;
                ClampTranslation();
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// Zoom relative to viewport center (used by keyboard shortcuts and toolbar buttons).
    /// </summary>
    private void ZoomRelative(double factor)
    {
        var target = _scale * factor;
        var center = new Point((_host?.ActualWidth ?? ActualWidth) / 2.0, (_host?.ActualHeight ?? ActualHeight) / 2.0);
        SetScale(target, center);
    }

    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (_scale < 1.5)
        {
            SetScale(2.0, e.GetPosition(this));
        }
        else
        {
            ResetView();
            CenterImage();
        }
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        var delta = point.Properties.MouseWheelDelta;
        if (delta == 0)
        {
            return;
        }

        var zoomIn = delta > 0;
        var factor = zoomIn ? 1.1 : 0.9;
        var newScale = _scale * factor;
        SetScale(newScale, point.Position);
        e.Handled = true;
    }

    /// <summary>
    /// Applies zoom so the image point under <paramref name="pivot"/> stays fixed.
    /// </summary>
    /// <remarks>
    /// The image element uses <c>RenderTransformOrigin="0.5,0.5"</c> and is centered
    /// in the viewport via layout alignment. This means the effective transform origin
    /// in viewport coordinates is the viewport center.
    ///
    /// The full mapping from image-local to viewport space is:
    ///   screen = viewportCenter + (imgLocal - imgCenter) × scale + translate
    ///
    /// Because the image is layout-centered, the viewport center acts as the origin
    /// for the transform group, giving us:
    ///   screen = origin + relativeOffset × scale + translate
    /// </remarks>
    private void SetScale(double targetScale, Point pivot)
    {
        targetScale = Math.Clamp(targetScale, MinScale, MaxScale);

        var prevScale = _scale;
        if (targetScale == prevScale)
        {
            return;
        }

        // The effective transform origin is the viewport center
        // (RenderTransformOrigin="0.5,0.5" + centered layout).
        var vw = _host?.ActualWidth ?? ActualWidth;
        var vh = _host?.ActualHeight ?? ActualHeight;
        var originX = vw / 2.0;
        var originY = vh / 2.0;

        // Convert pivot to image-relative-to-origin space using the old transform:
        //   pivot = origin + rel × oldScale + oldTranslate
        //   rel   = (pivot - origin - oldTranslate) / oldScale
        var relX = (pivot.X - originX - TranslateTransform.X) / prevScale;
        var relY = (pivot.Y - originY - TranslateTransform.Y) / prevScale;

        _scale = targetScale;
        ScaleTransform.ScaleX = _scale;
        ScaleTransform.ScaleY = _scale;

        // Solve for new translate so the same image point stays under the pivot:
        //   pivot = origin + rel × newScale + newTranslate
        //   newTranslate = pivot - origin - rel × newScale
        TranslateTransform.X = pivot.X - originX - (relX * _scale);
        TranslateTransform.Y = pivot.Y - originY - (relY * _scale);
        ClampTranslation();
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed)
        {
            _isPanning = true;
            _lastPanPoint = point.Position;
            CapturePointer(e.Pointer);
        }
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        var pos = point.Position;
        var dx = pos.X - _lastPanPoint.X;
        var dy = pos.Y - _lastPanPoint.Y;
        _lastPanPoint = pos;

        TranslateTransform.X += dx;
        TranslateTransform.Y += dy;
        ClampTranslation();
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            ReleasePointerCapture(e.Pointer);
        }
    }

    private void ClampTranslation()
    {
        var iw = ZoomImage.ActualWidth * ScaleTransform.ScaleX;
        var ih = ZoomImage.ActualHeight * ScaleTransform.ScaleY;
        var vw = _host?.ActualWidth ?? ActualWidth;
        var vh = _host?.ActualHeight ?? ActualHeight;
        if (iw <= 0 || ih <= 0 || vw <= 0 || vh <= 0)
        {
            return;
        }

        double maxOffsetX;
        double maxOffsetY;
        if (iw <= vw)
        {
            maxOffsetX = 0;
            TranslateTransform.X = 0;
        }
        else
        {
            maxOffsetX = Math.Max(0, ((iw - vw) / 2) + MinVisiblePadding);
        }

        if (ih <= vh)
        {
            maxOffsetY = 0;
            TranslateTransform.Y = 0;
        }
        else
        {
            maxOffsetY = Math.Max(0, ((ih - vh) / 2) + MinVisiblePadding);
        }

        if (TranslateTransform.X > maxOffsetX)
        {
            TranslateTransform.X = maxOffsetX;
        }

        if (TranslateTransform.X < -maxOffsetX)
        {
            TranslateTransform.X = -maxOffsetX;
        }

        if (TranslateTransform.Y > maxOffsetY)
        {
            TranslateTransform.Y = maxOffsetY;
        }

        if (TranslateTransform.Y < -maxOffsetY)
        {
            TranslateTransform.Y = -maxOffsetY;
        }
    }
}
