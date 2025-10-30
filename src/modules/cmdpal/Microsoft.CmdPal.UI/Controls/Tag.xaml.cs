// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Microsoft.CmdPal.UI.Controls;

[TemplatePart(Name = TagIconBox, Type = typeof(IconBox))]

public partial class Tag : Control
{
    internal const string TagIconBox = "PART_Icon";

    public OptionalColor? BackgroundColor
    {
        get => (OptionalColor?)GetValue(BackgroundColorProperty);
        set => SetValue(BackgroundColorProperty, value);
    }

    public OptionalColor? ForegroundColor
    {
        get => (OptionalColor?)GetValue(ForegroundColorProperty);
        set => SetValue(ForegroundColorProperty, value);
    }

    public OptionalColor? BorderBrushColor
    {
        get => (OptionalColor?)GetValue(BorderBrushColorProperty);
        set => SetValue(BorderBrushColorProperty, value);
    }

    public Microsoft.CommandPalette.Extensions.CornerRadius? CornerRadiusValue
    {
        get => (Microsoft.CommandPalette.Extensions.CornerRadius?)GetValue(CornerRadiusValueProperty);
        set => SetValue(CornerRadiusValueProperty, value);
    }

    public bool HasIcon => Icon?.HasIcon(this.ActualTheme == ElementTheme.Light) ?? false;

    public IconInfoViewModel? Icon
    {
        get => (IconInfoViewModel?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private static Brush? OriginalBg => Application.Current.Resources["TagBackground"] as Brush;

    private static Brush? OriginalFg => Application.Current.Resources["TagForeground"] as Brush;

    private static Brush? OriginalBorder => Application.Current.Resources["TagBorderBrush"] as Brush;

    private static CornerRadius? OriginalCornerRadius => Application.Current.Resources["TagCornerRadius"] as CornerRadius?;

    public static readonly DependencyProperty ForegroundColorProperty =
        DependencyProperty.Register(nameof(ForegroundColor), typeof(OptionalColor), typeof(Tag), new PropertyMetadata(null, OnForegroundColorPropertyChanged));

    public static readonly DependencyProperty BackgroundColorProperty =
        DependencyProperty.Register(nameof(BackgroundColor), typeof(OptionalColor), typeof(Tag), new PropertyMetadata(null, OnBackgroundColorPropertyChanged));

    public static readonly DependencyProperty BorderBrushColorProperty =
        DependencyProperty.Register(nameof(BorderBrushColor), typeof(OptionalColor), typeof(Tag), new PropertyMetadata(null, OnBorderBrushColorPropertyChanged));

    public static readonly DependencyProperty CornerRadiusValueProperty =
        DependencyProperty.Register(nameof(CornerRadiusValue), typeof(Microsoft.CommandPalette.Extensions.CornerRadius?), typeof(Tag), new PropertyMetadata(null, OnCornerRadiusValuePropertyChanged));

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(IconInfoViewModel), typeof(Tag), new PropertyMetadata(null));

    public static readonly DependencyProperty TextProperty =
    DependencyProperty.Register(nameof(Text), typeof(string), typeof(Tag), new PropertyMetadata(null));

    public Tag()
    {
        this.DefaultStyleKey = typeof(Tag);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (GetTemplateChild(TagIconBox) is IconBox iconBox)
        {
            iconBox.SourceRequested += IconCacheProvider.SourceRequested;
            iconBox.Visibility = HasIcon ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static void OnForegroundColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Tag tag)
        {
            return;
        }

        if (tag.ForegroundColor is not null &&
            OptionalColorBrushCacheProvider.Convert(tag.ForegroundColor.Value) is SolidColorBrush brush)
        {
            tag.Foreground = brush;
        }
        else
        {
            tag.Foreground = OriginalFg;
        }
    }

    private static void OnBackgroundColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Tag tag)
        {
            return;
        }

        if (tag.BackgroundColor is not null &&
            OptionalColorBrushCacheProvider.Convert(tag.BackgroundColor.Value) is SolidColorBrush brush)
        {
            tag.Background = brush;
        }
        else
        {
            tag.Background = OriginalBg;
        }
    }

    private static void OnBorderBrushColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Tag tag)
        {
            return;
        }

        if (tag.BorderBrushColor is not null &&
            OptionalColorBrushCacheProvider.Convert(tag.BorderBrushColor.Value) is SolidColorBrush brush)
        {
            tag.BorderBrush = brush;
        }
        else
        {
            tag.BorderBrush = OriginalBorder;
        }
    }

    private static void OnCornerRadiusValuePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Tag tag)
        {
            return;
        }

        if (tag.CornerRadiusValue is Microsoft.CommandPalette.Extensions.CornerRadius extensionRadius)
        {
            tag.CornerRadius = new CornerRadius(extensionRadius.TopLeft, extensionRadius.TopRight, extensionRadius.BottomRight, extensionRadius.BottomLeft);
        }
        else
        {
            // Use TagCornerRadius from theme resources
            if (OriginalCornerRadius is CornerRadius defaultRadius)
            {
                tag.CornerRadius = defaultRadius;
            }
        }
    }
}
