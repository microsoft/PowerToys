// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class CommandPalettePreview : UserControl
{
    public static readonly DependencyProperty PreviewBackgroundOpacityProperty = DependencyProperty.Register(nameof(PreviewBackgroundOpacity), typeof(double), typeof(CommandPalettePreview), new PropertyMetadata(0d));

    public static readonly DependencyProperty PreviewBackgroundColorProperty = DependencyProperty.Register(nameof(PreviewBackgroundColor), typeof(Color), typeof(CommandPalettePreview), new PropertyMetadata(default(Color)));

    public static readonly DependencyProperty PreviewBackgroundImageSourceProperty = DependencyProperty.Register(nameof(PreviewBackgroundImageSource), typeof(ImageSource), typeof(CommandPalettePreview), new PropertyMetadata(null, PropertyChangedCallback));

    public static readonly DependencyProperty PreviewBackgroundImageOpacityProperty = DependencyProperty.Register(nameof(PreviewBackgroundImageOpacity), typeof(int), typeof(CommandPalettePreview), new PropertyMetadata(0));

    public static readonly DependencyProperty PreviewBackgroundImageFitProperty = DependencyProperty.Register(nameof(PreviewBackgroundImageFit), typeof(BackgroundImageFit), typeof(CommandPalettePreview), new PropertyMetadata(default(BackgroundImageFit)));

    public static readonly DependencyProperty PreviewBackgroundImageBrightnessProperty = DependencyProperty.Register(nameof(PreviewBackgroundImageBrightness), typeof(double), typeof(CommandPalettePreview), new PropertyMetadata(0d));

    public static readonly DependencyProperty PreviewBackgroundImageBlurAmountProperty = DependencyProperty.Register(nameof(PreviewBackgroundImageBlurAmount), typeof(double), typeof(CommandPalettePreview), new PropertyMetadata(0d));

    public static readonly DependencyProperty PreviewBackgroundImageTintProperty = DependencyProperty.Register(nameof(PreviewBackgroundImageTint), typeof(Color), typeof(CommandPalettePreview), new PropertyMetadata(default(Color)));

    public static readonly DependencyProperty PreviewBackgroundImageTintIntensityProperty = DependencyProperty.Register(nameof(PreviewBackgroundImageTintIntensity), typeof(int), typeof(CommandPalettePreview), new PropertyMetadata(0));

    public static readonly DependencyProperty ShowBackgroundImageProperty = DependencyProperty.Register(nameof(ShowBackgroundImage), typeof(Visibility), typeof(CommandPalettePreview), new PropertyMetadata(Visibility.Collapsed));

    public BackgroundImageFit PreviewBackgroundImageFit
    {
        get { return (BackgroundImageFit)GetValue(PreviewBackgroundImageFitProperty); }
        set { SetValue(PreviewBackgroundImageFitProperty, value); }
    }

    public double PreviewBackgroundOpacity
    {
        get { return (double)GetValue(PreviewBackgroundOpacityProperty); }
        set { SetValue(PreviewBackgroundOpacityProperty, value); }
    }

    public Color PreviewBackgroundColor
    {
        get { return (Color)GetValue(PreviewBackgroundColorProperty); }
        set { SetValue(PreviewBackgroundColorProperty, value); }
    }

    public ImageSource PreviewBackgroundImageSource
    {
        get { return (ImageSource)GetValue(PreviewBackgroundImageSourceProperty); }
        set { SetValue(PreviewBackgroundImageSourceProperty, value); }
    }

    public int PreviewBackgroundImageOpacity
    {
        get { return (int)GetValue(PreviewBackgroundImageOpacityProperty); }
        set { SetValue(PreviewBackgroundImageOpacityProperty, value); }
    }

    public double PreviewBackgroundImageBrightness
    {
        get => (double)GetValue(PreviewBackgroundImageBrightnessProperty);
        set => SetValue(PreviewBackgroundImageBrightnessProperty, value);
    }

    public double PreviewBackgroundImageBlurAmount
    {
        get => (double)GetValue(PreviewBackgroundImageBlurAmountProperty);
        set => SetValue(PreviewBackgroundImageBlurAmountProperty, value);
    }

    public Color PreviewBackgroundImageTint
    {
        get => (Color)GetValue(PreviewBackgroundImageTintProperty);
        set => SetValue(PreviewBackgroundImageTintProperty, value);
    }

    public int PreviewBackgroundImageTintIntensity
    {
        get => (int)GetValue(PreviewBackgroundImageTintIntensityProperty);
        set => SetValue(PreviewBackgroundImageTintIntensityProperty, value);
    }

    public Visibility ShowBackgroundImage
    {
        get => (Visibility)GetValue(ShowBackgroundImageProperty);
        set => SetValue(ShowBackgroundImageProperty, value);
    }

    public CommandPalettePreview()
    {
        InitializeComponent();
    }

    private static void PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not CommandPalettePreview preview)
        {
            return;
        }

        preview.ShowBackgroundImage = e.NewValue is ImageSource ? Visibility.Visible : Visibility.Collapsed;
    }

    private double ToOpacity(int value) => value / 100.0;

    private double ToTintIntensity(int value) => value / 100.0;

    private Stretch ToStretch(BackgroundImageFit fit)
    {
        return fit switch
        {
            BackgroundImageFit.Fill => Stretch.Fill,
            BackgroundImageFit.UniformToFill => Stretch.UniformToFill,
            _ => Stretch.None,
        };
    }
}
