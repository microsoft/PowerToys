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
    public static readonly DependencyProperty PreviewBackgroundOpacityProperty = DependencyProperty.Register(nameof(PreviewBackgroundOpacity), typeof(double), typeof(CommandPalettePreview), new PropertyMetadata(default(double)));

    public static readonly DependencyProperty PreviewBackgroundColorProperty = DependencyProperty.Register(nameof(PreviewBackgroundColor), typeof(Color), typeof(CommandPalettePreview), new PropertyMetadata(default(Color)));

    public static readonly DependencyProperty PreviewBackgroundImageSourceProperty = DependencyProperty.Register(nameof(PreviewBackgroundImageSource), typeof(ImageSource), typeof(CommandPalettePreview), new PropertyMetadata(default(ImageSource)));

    public static readonly DependencyProperty PreviewBackgroundImageOpacityProperty = DependencyProperty.Register(nameof(PreviewBackgroundImageOpacity), typeof(int), typeof(CommandPalettePreview), new PropertyMetadata(default(int)));

    public static readonly DependencyProperty PreviewBackgroundImageFitProperty = DependencyProperty.Register(nameof(PreviewBackgroundImageFit), typeof(BackgroundImageFit), typeof(CommandPalettePreview), new PropertyMetadata(default(BackgroundImageFit)));

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

    public CommandPalettePreview()
    {
        InitializeComponent();
    }

    public double ToOpacity(int value) => value / 100.0;

    public Stretch ToStretch(BackgroundImageFit fit)
    {
        return fit switch
        {
            BackgroundImageFit.Fill => Stretch.Fill,
            BackgroundImageFit.UniformToFill => Stretch.UniformToFill,
            _ => Stretch.None,
        };
    }
}
