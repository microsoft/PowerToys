// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
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

    public static readonly DependencyProperty ForegroundColorProperty =
        DependencyProperty.Register(nameof(ForegroundColor), typeof(OptionalColor), typeof(Tag), new PropertyMetadata(null, OnForegroundColorPropertyChanged));

    public static readonly DependencyProperty BackgroundColorProperty =
        DependencyProperty.Register(nameof(BackgroundColor), typeof(OptionalColor), typeof(Tag), new PropertyMetadata(null, OnBackgroundColorPropertyChanged));

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

        if (tag.ForegroundColor != null &&
            OptionalColorBrushCacheProvider.Convert(tag.ForegroundColor.Value) is SolidColorBrush brush)
        {
            tag.Foreground = brush;

            // If we have a BG color, then don't apply a border.
            if (tag.BackgroundColor is OptionalColor bg && bg.HasValue)
            {
                tag.BorderBrush = OriginalBorder;
            }
            else
            {
                // Otherwise (no background), use the FG as the border
                tag.BorderBrush = brush;
            }
        }
        else
        {
            tag.Foreground = OriginalFg;
            tag.BorderBrush = OriginalBorder;
        }
    }

    private static void OnBackgroundColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Tag tag)
        {
            return;
        }

        if (tag.BackgroundColor != null &&
            OptionalColorBrushCacheProvider.Convert(tag.BackgroundColor.Value) is SolidColorBrush brush)
        {
            tag.Background = brush;

            // Since we have a BG here, we never want a border.
            tag.BorderBrush = OriginalBorder;

            // If we have a FG color, then don't apply a border.
            if (tag.ForegroundColor is OptionalColor fg && fg.HasValue)
            {
                tag.BorderBrush = OriginalBorder;
            }
            else
            {
                // Otherwise (no foreground), use the FG as the border
                tag.BorderBrush = brush;
            }
        }
        else
        {
            // No BG color here.
            tag.Background = OriginalBg;

            // If we have a FG color, then don't apply a border.
            if (tag.ForegroundColor is OptionalColor fg && fg.HasValue)
            {
                tag.BorderBrush = tag.Foreground;
            }
            else
            {
                // Otherwise (no foreground), use the FG as the border
                tag.BorderBrush = OriginalBorder;
            }
        }
    }
}
