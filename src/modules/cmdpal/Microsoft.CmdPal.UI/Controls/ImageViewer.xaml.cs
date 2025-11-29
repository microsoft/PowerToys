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
    private Grid? _host;

    private Point _lastPanPoint;
    private bool _isPanning;
    private double _scale = 1.0;
    private const double MinScale = 0.25;
    private const double MaxScale = 8.0;
    private const double MinVisiblePadding = 24.0;
    private const double KeyboardPanStep = 24.0;

    public event EventHandler? CancelRequested;

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

    private void SetScale(double targetScale, Point pivot)
    {
        if (targetScale < MinScale)
        {
            targetScale = MinScale;
        }
        else if (targetScale > MaxScale)
        {
            targetScale = MaxScale;
        }

        var prevScale = _scale;
        _scale = targetScale;
        ScaleTransform.ScaleX = _scale;
        ScaleTransform.ScaleY = _scale;

        var dx = pivot.X - TranslateTransform.X;
        var dy = pivot.Y - TranslateTransform.Y;
        var scaleRatio = _scale / prevScale;
        TranslateTransform.X -= dx * (scaleRatio - 1);
        TranslateTransform.Y -= dy * (scaleRatio - 1);
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
