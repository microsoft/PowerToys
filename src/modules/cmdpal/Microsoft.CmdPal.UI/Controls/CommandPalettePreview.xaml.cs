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
    public static readonly DependencyProperty PreviewBackgroundColorProperty = DependencyProperty.Register(nameof(PreviewBackgroundColor), typeof(Color), typeof(CommandPalettePreview), new PropertyMetadata(default(Color), OnBackdropPropertyChanged));

    public static readonly DependencyProperty PreviewBackgroundImageSourceProperty = DependencyProperty.Register(nameof(PreviewBackgroundImageSource), typeof(ImageSource), typeof(CommandPalettePreview), new PropertyMetadata(null, OnBackgroundImageSourceChanged));

    public static readonly DependencyProperty PreviewBackgroundImageOpacityProperty = DependencyProperty.Register(nameof(PreviewBackgroundImageOpacity), typeof(double), typeof(CommandPalettePreview), new PropertyMetadata(1.0));

    public static readonly DependencyProperty PreviewBackgroundImageFitProperty = DependencyProperty.Register(nameof(PreviewBackgroundImageFit), typeof(BackgroundImageFit), typeof(CommandPalettePreview), new PropertyMetadata(default(BackgroundImageFit)));

    public static readonly DependencyProperty PreviewBackgroundImageBrightnessProperty = DependencyProperty.Register(nameof(PreviewBackgroundImageBrightness), typeof(double), typeof(CommandPalettePreview), new PropertyMetadata(0d));

    public static readonly DependencyProperty PreviewBackgroundImageBlurAmountProperty = DependencyProperty.Register(nameof(PreviewBackgroundImageBlurAmount), typeof(double), typeof(CommandPalettePreview), new PropertyMetadata(0d));

    public static readonly DependencyProperty PreviewBackgroundImageTintProperty = DependencyProperty.Register(nameof(PreviewBackgroundImageTint), typeof(Color), typeof(CommandPalettePreview), new PropertyMetadata(default(Color)));

    public static readonly DependencyProperty PreviewBackgroundImageTintIntensityProperty = DependencyProperty.Register(nameof(PreviewBackgroundImageTintIntensity), typeof(int), typeof(CommandPalettePreview), new PropertyMetadata(0));

    public static readonly DependencyProperty ShowBackgroundImageProperty = DependencyProperty.Register(nameof(ShowBackgroundImage), typeof(Visibility), typeof(CommandPalettePreview), new PropertyMetadata(Visibility.Collapsed, OnVisibilityPropertyChanged));

    public static readonly DependencyProperty PreviewBackdropStyleProperty = DependencyProperty.Register(nameof(PreviewBackdropStyle), typeof(BackdropStyle?), typeof(CommandPalettePreview), new PropertyMetadata(null, OnVisibilityPropertyChanged));

    public static readonly DependencyProperty PreviewEffectiveOpacityProperty = DependencyProperty.Register(nameof(PreviewEffectiveOpacity), typeof(double), typeof(CommandPalettePreview), new PropertyMetadata(1.0, OnBackdropPropertyChanged));

    // Computed read-only dependency properties
    public static readonly DependencyProperty EffectiveClearColorProperty = DependencyProperty.Register(nameof(EffectiveClearColor), typeof(Color), typeof(CommandPalettePreview), new PropertyMetadata(default(Color)));

    public static readonly DependencyProperty AcrylicVisibilityProperty = DependencyProperty.Register(nameof(AcrylicVisibility), typeof(Visibility), typeof(CommandPalettePreview), new PropertyMetadata(Visibility.Visible));

    public static readonly DependencyProperty ClearVisibilityProperty = DependencyProperty.Register(nameof(ClearVisibility), typeof(Visibility), typeof(CommandPalettePreview), new PropertyMetadata(Visibility.Collapsed));

    public BackgroundImageFit PreviewBackgroundImageFit
    {
        get { return (BackgroundImageFit)GetValue(PreviewBackgroundImageFitProperty); }
        set { SetValue(PreviewBackgroundImageFitProperty, value); }
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

    public double PreviewBackgroundImageOpacity
    {
        get => (double)GetValue(PreviewBackgroundImageOpacityProperty);
        set => SetValue(PreviewBackgroundImageOpacityProperty, value);
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

    public BackdropStyle? PreviewBackdropStyle
    {
        get => (BackdropStyle?)GetValue(PreviewBackdropStyleProperty);
        set => SetValue(PreviewBackdropStyleProperty, value);
    }

    /// <summary>
    /// Gets or sets the effective opacity for the backdrop, pre-computed by the theme provider.
    /// For Acrylic style: used directly as TintOpacity.
    /// For Clear style: used to compute the alpha channel of the solid color.
    /// </summary>
    public double PreviewEffectiveOpacity
    {
        get => (double)GetValue(PreviewEffectiveOpacityProperty);
        set => SetValue(PreviewEffectiveOpacityProperty, value);
    }

    // Computed read-only properties
    public Color EffectiveClearColor
    {
        get => (Color)GetValue(EffectiveClearColorProperty);
        private set => SetValue(EffectiveClearColorProperty, value);
    }

    public Visibility AcrylicVisibility
    {
        get => (Visibility)GetValue(AcrylicVisibilityProperty);
        private set => SetValue(AcrylicVisibilityProperty, value);
    }

    public Visibility ClearVisibility
    {
        get => (Visibility)GetValue(ClearVisibilityProperty);
        private set => SetValue(ClearVisibilityProperty, value);
    }

    public CommandPalettePreview()
    {
        InitializeComponent();
    }

    private static void OnBackgroundImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not CommandPalettePreview preview)
        {
            return;
        }

        preview.ShowBackgroundImage = e.NewValue is ImageSource ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void OnBackdropPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not CommandPalettePreview preview)
        {
            return;
        }

        preview.UpdateComputedClearColor();
    }

    private static void OnVisibilityPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not CommandPalettePreview preview)
        {
            return;
        }

        preview.UpdateComputedVisibilityProperties();
        preview.UpdateComputedClearColor();
    }

    private void UpdateComputedClearColor()
    {
        EffectiveClearColor = Color.FromArgb(
            (byte)(PreviewEffectiveOpacity * 255),
            PreviewBackgroundColor.R,
            PreviewBackgroundColor.G,
            PreviewBackgroundColor.B);
    }

    private void UpdateComputedVisibilityProperties()
    {
        var config = BackdropStyles.Get(PreviewBackdropStyle ?? BackdropStyle.Acrylic);

        // Show backdrop effect based on style (on top of any background image)
        AcrylicVisibility = config.PreviewBrush == PreviewBrushKind.Acrylic
            ? Visibility.Visible : Visibility.Collapsed;
        ClearVisibility = config.PreviewBrush == PreviewBrushKind.Solid
            ? Visibility.Visible : Visibility.Collapsed;
    }

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
