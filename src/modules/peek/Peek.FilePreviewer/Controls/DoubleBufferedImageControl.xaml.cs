// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Peek.FilePreviewer.Models;
using Windows.Foundation;

namespace Peek.FilePreviewer.Controls;

/// <summary>
/// Displays an image using two overlaid <see cref="Image"/> elements and keeps the next
/// frame in a back buffer so the visible frame retains its source and bounds until
/// the replacement is promoted. Window resizing is not synchronized with this swap.
/// </summary>
public sealed partial class DoubleBufferedImageControl : UserControl
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source),
        typeof(ImageSource),
        typeof(DoubleBufferedImageControl),
        new PropertyMetadata(null, OnSourceChanged));

    private readonly ImagePreviewBuffer _buffer = new();

    /// <summary>The image currently visible to the user.</summary>
    private Image _currentImage;

    /// <summary>The back buffer, which receives the next image before it is promoted.</summary>
    private Image _hiddenImage;

    private double _scalingFactor = 1.0;

    public DoubleBufferedImageControl()
    {
        InitializeComponent();

        _currentImage = Image1;
        _hiddenImage = Image2;
    }

    public ImageSource? Source
    {
        get => (ImageSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public double ScalingFactor
    {
        get => _scalingFactor;
        set
        {
            _scalingFactor = value;
            ApplyFrame(_currentImage, _buffer.Current);
            ApplyFrame(_hiddenImage, _buffer.Next);
        }
    }

    public void Clear()
    {
        if (Source is not null)
        {
            Source = null;
        }
        else
        {
            UpdateSource(null);
        }
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((DoubleBufferedImageControl)d).UpdateSource(e.NewValue as ImageSource);
    }

    private void UpdateSource(ImageSource? newSource)
    {
        if (newSource is null)
        {
            _buffer.Clear();
            Image1.Source = null;
            Image2.Source = null;
            Image1.Opacity = 1;
            Image2.Opacity = 0;
            _currentImage = Image1;
            _hiddenImage = Image2;
            return;
        }

        Size? pixelSize = newSource is BitmapImage bitmap
            ? new Size(bitmap.PixelWidth, bitmap.PixelHeight)
            : null;
        PrepareNextImage(new ImagePreviewFrame(newSource, pixelSize));
        if (_buffer.Current is null)
        {
            InstantSwap();
        }
    }

    /// <summary>
    /// Stages an already-loaded source/dimension snapshot in the back buffer.
    /// </summary>
    public void PrepareNextImage(ImagePreviewFrame? frame)
    {
        if (frame is null)
        {
            Clear();
            return;
        }

        // Keep the visible frame's bounds unchanged until the staged frame is promoted.
        _buffer.Prepare(frame);

        ApplyFrame(_hiddenImage, _buffer.Next);
        _hiddenImage.Opacity = 0;
        _currentImage.Opacity = 1;
    }

    /// <summary>
    /// Promotes the back buffer immediately.
    /// </summary>
    public void InstantSwap()
    {
        if (!_buffer.Swap())
        {
            return;
        }

        (_currentImage, _hiddenImage) = (_hiddenImage, _currentImage);
        _currentImage.Opacity = 1;
        _hiddenImage.Opacity = 0;
        _hiddenImage.Source = null;
    }

    private void ApplyFrame(Image image, ImagePreviewFrame? frame)
    {
        var maxSize = frame?.GetMaxSize(ScalingFactor)
            ?? new Size(double.PositiveInfinity, double.PositiveInfinity);
        image.MaxWidth = maxSize.Width;
        image.MaxHeight = maxSize.Height;
        image.Source = frame?.Source;
    }
}
