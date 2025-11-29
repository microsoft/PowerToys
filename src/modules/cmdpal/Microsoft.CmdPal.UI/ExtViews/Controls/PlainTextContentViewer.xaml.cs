// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Microsoft.CmdPal.UI.ExtViews.Controls;

public sealed partial class PlainTextContentViewer : UserControl
{
    public static readonly DependencyProperty WordWrapEnabledProperty = DependencyProperty.Register(
        nameof(WordWrapEnabled), typeof(bool), typeof(PlainTextContentViewer), new PropertyMetadata(false, OnWrapChanged));

    public static readonly DependencyProperty UseMonospaceProperty = DependencyProperty.Register(
        nameof(UseMonospace), typeof(bool), typeof(PlainTextContentViewer), new PropertyMetadata(false, OnFontChanged));

    public bool WordWrapEnabled
    {
        get => (bool)GetValue(WordWrapEnabledProperty);
        set => SetValue(WordWrapEnabledProperty, value);
    }

    public bool UseMonospace
    {
        get => (bool)GetValue(UseMonospaceProperty);
        set => SetValue(UseMonospaceProperty, value);
    }

    public PlainTextContentViewer()
    {
        InitializeComponent();
        UpdateFont();
    }

    private static void OnFontChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => (d as PlainTextContentViewer)?.UpdateFont();

    private static void OnWrapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => (d as PlainTextContentViewer)?.UpdateWordWrap();

    private void UpdateWordWrap()
    {
        ContentTextBlock.TextWrapping = WordWrapEnabled ? TextWrapping.Wrap : TextWrapping.NoWrap;
        Scroller.HorizontalScrollBarVisibility = WordWrapEnabled ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;

        // Force a layout update to ensure the font change takes effect immediately
        UpdateLayout();
    }

    private void UpdateFont()
    {
        if (ContentTextBlock is null)
        {
            return;
        }

        try
        {
            ContentTextBlock.FontFamily = UseMonospace ? new FontFamily("Consolas") : FontFamily.XamlAutoFontFamily;
        }
        catch
        {
            ContentTextBlock.FontFamily = FontFamily.XamlAutoFontFamily;
        }

        // Force a layout update to ensure the font change takes effect immediately
        ContentTextBlock.UpdateLayout();
    }

    private void CopySelection_Click(object sender, RoutedEventArgs e)
    {
        var txt = string.IsNullOrEmpty(ContentTextBlock?.SelectedText) ? ContentTextBlock?.Text : ContentTextBlock?.SelectedText;
        if (!string.IsNullOrEmpty(txt))
        {
            ClipboardHelper.SetText(txt);
        }
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        ContentTextBlock.SelectAll();
    }
}
