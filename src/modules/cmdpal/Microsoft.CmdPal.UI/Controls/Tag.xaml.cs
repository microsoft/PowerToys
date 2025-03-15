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
        if (d is not Tag tag || tag.ForegroundColor is null || !tag.ForegroundColor.HasValue)
        {
            return;
        }

        if (OptionalColorBrushCacheProvider.Convert(tag.ForegroundColor.Value) is SolidColorBrush brush)
        {
            tag.Foreground = brush;
            tag.BorderBrush = brush;
        }
    }

    private static void OnBackgroundColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Tag tag || tag.BackgroundColor is null || !tag.BackgroundColor.HasValue)
        {
            return;
        }

        if (OptionalColorBrushCacheProvider.Convert(tag.BackgroundColor.Value) is SolidColorBrush brush)
        {
            tag.Background = brush;
        }
    }
}
