// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using Windows.UI.Core;

namespace Microsoft.CmdPal.UI.ExtViews.Controls;

public sealed partial class PlainTextContentViewer : UserControl
{
    private const double MinFontSize = 8.0;
    private const double MaxFontSize = 72.0;
    private const double FontSizeStep = 2.0;

    private double _defaultFontSize;
    private double _fontSize;

    public static readonly DependencyProperty WordWrapEnabledProperty = DependencyProperty.Register(
        nameof(WordWrapEnabled), typeof(bool), typeof(PlainTextContentViewer), new PropertyMetadata(false, OnWrapChanged));

    public static readonly DependencyProperty UseMonospaceProperty = DependencyProperty.Register(
        nameof(UseMonospace), typeof(bool), typeof(PlainTextContentViewer), new PropertyMetadata(false, OnFontChanged));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(PlainTextContentViewer), new PropertyMetadata(default(string), OnTextChanged));

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

    public string Text
    {
        get { return (string)GetValue(TextProperty); }
        set { SetValue(TextProperty, value); }
    }

    public PlainTextContentViewer()
    {
        InitializeComponent();
        UpdateFont();

        _defaultFontSize = ContentTextBlock.FontSize;
        _fontSize = _defaultFontSize;

        IsTabStop = true;
        KeyDown += OnKeyDown;
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => (d as PlainTextContentViewer)?.UpdateText();

    private static void OnFontChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => (d as PlainTextContentViewer)?.UpdateFont();

    private static void OnWrapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => (d as PlainTextContentViewer)?.UpdateWordWrap();

    private void UpdateText()
    {
        if (ContentTextBlock is null)
        {
            return;
        }

        ContentTextBlock.Text = Text;
    }

    private void UpdateWordWrap()
    {
        ContentTextBlock.TextWrapping = WordWrapEnabled ? TextWrapping.Wrap : TextWrapping.NoWrap;
        Scroller.HorizontalScrollBarVisibility = WordWrapEnabled ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
        InvalidateLayout();
    }

    private void UpdateFont()
    {
        if (ContentTextBlock is null)
        {
            return;
        }

        try
        {
            ContentTextBlock.FontFamily = UseMonospace ? new FontFamily("Cascadia Mono, Consolas") : FontFamily.XamlAutoFontFamily;
        }
        catch
        {
            ContentTextBlock.FontFamily = FontFamily.XamlAutoFontFamily;
        }

        InvalidateLayout();
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

    private void ZoomIn()
    {
        _fontSize = Math.Min(_fontSize + FontSizeStep, MaxFontSize);
        ApplyFontSize();
    }

    private void ZoomOut()
    {
        _fontSize = Math.Max(_fontSize - FontSizeStep, MinFontSize);
        ApplyFontSize();
    }

    private void ResetZoom()
    {
        _fontSize = _defaultFontSize;
        ApplyFontSize();
    }

    private void ApplyFontSize()
    {
        ContentTextBlock.FontSize = _fontSize;
        InvalidateLayout();
    }

    /// <summary>
    /// Changing font properties on a TextBlock inside a ScrollViewer can leave
    /// stale layout state, causing the text to disappear until the next
    /// interaction. Re-setting the Text property forces the TextBlock to
    /// discard its cached layout and fully re-render.
    /// </summary>
    private void InvalidateLayout()
    {
        var text = ContentTextBlock.Text;
        ContentTextBlock.Text = string.Empty;
        ContentTextBlock.Text = text;
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => ZoomIn();

    private void ZoomOut_Click(object sender, RoutedEventArgs e) => ZoomOut();

    private void ResetZoom_Click(object sender, RoutedEventArgs e) => ResetZoom();

    private static bool IsCtrlDown()
    {
        var state = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
        return (state & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!IsCtrlDown())
        {
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.Add:
            case (VirtualKey)187: // =/+ key
                ZoomIn();
                e.Handled = true;
                break;
            case VirtualKey.Subtract:
            case (VirtualKey)189: // -/_ key
                ZoomOut();
                e.Handled = true;
                break;
            case VirtualKey.Number0:
            case VirtualKey.NumberPad0:
                ResetZoom();
                e.Handled = true;
                break;
        }
    }

    private void ContentTextBlock_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (!IsCtrlDown())
        {
            return;
        }

        var point = e.GetCurrentPoint(ContentTextBlock);
        var delta = point.Properties.MouseWheelDelta;
        if (delta > 0)
        {
            ZoomIn();
        }
        else if (delta < 0)
        {
            ZoomOut();
        }

        e.Handled = true;
    }
}
