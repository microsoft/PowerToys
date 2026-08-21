// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Peek.FilePreviewer.Controls;

/// <summary>
/// Displays an image using two overlaid <see cref="Image"/> elements and keeps the next frame in
/// a back buffer so transitions can swap immediately without tearing or flashing.
/// </summary>
public sealed partial class DoubleBufferedImageControl : UserControl
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source),
        typeof(ImageSource),
        typeof(DoubleBufferedImageControl),
        new PropertyMetadata(null, OnSourceChanged));

    /// <summary>The image currently visible to the user.</summary>
    private Image _currentImage;

    /// <summary>The back buffer, which receives the next image before it is promoted.</summary>
    private Image _hiddenImage;

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

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((DoubleBufferedImageControl)d).UpdateSource(e.NewValue as ImageSource);
    }

    private void UpdateSource(ImageSource? newSource)
    {
        if (newSource is null)
        {
            Image1.Source = null;
            Image2.Source = null;
            Image1.Opacity = 1;
            Image2.Opacity = 0;
            _currentImage = Image1;
            _hiddenImage = Image2;
            return;
        }

        if (_currentImage.Source is null)
        {
            // First load. Directly display the current image.
            _currentImage.Source = newSource;
            _currentImage.Opacity = 1;
            _hiddenImage.Source = null;
            _hiddenImage.Opacity = 0;
            return;
        }

        // Active image visible: prepare back buffer for immediate promotion.
        _currentImage.Opacity = 1;
        _hiddenImage.Opacity = 0;
        _hiddenImage.Source = newSource;
    }

    /// <summary>
    /// Promotes the back buffer immediately without animation.
    /// </summary>
    public void InstantSwap()
    {
        if (_hiddenImage.Source is null)
        {
            return;
        }

        PromoteHiddenImage();
    }

    private void PromoteHiddenImage()
    {
        (_currentImage, _hiddenImage) = (_hiddenImage, _currentImage);
        _currentImage.Opacity = 1;
        _hiddenImage.Opacity = 0;
        _hiddenImage.Source = null;
    }
}
