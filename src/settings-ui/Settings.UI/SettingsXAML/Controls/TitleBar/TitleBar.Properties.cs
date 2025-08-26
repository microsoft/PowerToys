// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Controls;

public partial class TitleBar : Control
{
    /// <summary>
    /// The backing <see cref="DependencyProperty"/> for the <see cref="Icon"/> property.
    /// </summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(IconElement), typeof(TitleBar), new PropertyMetadata(null, IconChanged));

    /// <summary>
    /// The backing <see cref="DependencyProperty"/> for the <see cref="Title"/> property.
    /// </summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(TitleBar), new PropertyMetadata(default(string)));

    /// <summary>
    /// The backing <see cref="DependencyProperty"/> for the <see cref="Subtitle"/> property.
    /// </summary>
    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(TitleBar), new PropertyMetadata(default(string)));

    /// <summary>
    /// The backing <see cref="DependencyProperty"/> for the <see cref="Content"/> property.
    /// </summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(nameof(Content), typeof(object), typeof(TitleBar), new PropertyMetadata(null, ContentChanged));

    /// <summary>
    /// The backing <see cref="DependencyProperty"/> for the <see cref="Footer"/> property.
    /// </summary>
    public static readonly DependencyProperty FooterProperty = DependencyProperty.Register(nameof(Footer), typeof(object), typeof(TitleBar), new PropertyMetadata(null, FooterChanged));

    /// <summary>
    /// The backing <see cref="DependencyProperty"/> for the <see cref="IsBackButtonVisible"/> property.
    /// </summary>
    public static readonly DependencyProperty IsBackButtonVisibleProperty = DependencyProperty.Register(nameof(IsBackButtonVisible), typeof(bool), typeof(TitleBar), new PropertyMetadata(false, IsBackButtonVisibleChanged));

    /// <summary>
    /// The backing <see cref="DependencyProperty"/> for the <see cref="IsPaneButtonVisible"/> property.
    /// </summary>
    public static readonly DependencyProperty IsPaneButtonVisibleProperty = DependencyProperty.Register(nameof(IsPaneButtonVisible), typeof(bool), typeof(TitleBar), new PropertyMetadata(false, IsPaneButtonVisibleChanged));

    /// <summary>
    /// The backing <see cref="DependencyProperty"/> for the <see cref="DisplayMode"/> property.
    /// </summary>
    public static readonly DependencyProperty DisplayModeProperty = DependencyProperty.Register(nameof(DisplayMode), typeof(DisplayMode), typeof(TitleBar), new PropertyMetadata(DisplayMode.Standard, DisplayModeChanged));

    /// <summary>
    /// The backing <see cref="DependencyProperty"/> for the <see cref="CompactStateBreakpoint
    /// "/> property.
    /// </summary>
    public static readonly DependencyProperty CompactStateBreakpointProperty = DependencyProperty.Register(nameof(CompactStateBreakpoint), typeof(int), typeof(TitleBar), new PropertyMetadata(850));

    /// <summary>
    /// The backing <see cref="DependencyProperty"/> for the <see cref="AutoConfigureCustomTitleBar"/> property.
    /// </summary>
    public static readonly DependencyProperty AutoConfigureCustomTitleBarProperty = DependencyProperty.Register(nameof(AutoConfigureCustomTitleBar), typeof(bool), typeof(TitleBar), new PropertyMetadata(true, AutoConfigureCustomTitleBarChanged));

    /// <summary>
    /// The backing <see cref="DependencyProperty"/> for the <see cref="Window"/> property.
    /// </summary>
    public static readonly DependencyProperty WindowProperty = DependencyProperty.Register(nameof(Window), typeof(Window), typeof(TitleBar), new PropertyMetadata(null));

    /// <summary>
    /// The event that gets fired when the back button is clicked
    /// </summary>
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
    public event EventHandler<RoutedEventArgs>? BackButtonClick;
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.

    /// <summary>
    /// The event that gets fired when the pane toggle button is clicked
    /// </summary>
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
    public event EventHandler<RoutedEventArgs>? PaneButtonClick;
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.

    /// <summary>
    /// Gets or sets the Icon
    /// </summary>
    public IconElement Icon
    {
        get => (IconElement)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>
    /// Gets or sets the Title
    /// </summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// Gets or sets the Subtitle
    /// </summary>
    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <summary>
    /// Gets or sets the content shown at the center of the TitleBar. When setting this, using DisplayMode=Tall is recommended.
    /// </summary>
    public object Content
    {
        get => (object)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>
    /// Gets or sets the content shown at the right of the TitleBar, next to the caption buttons. When setting this, using DisplayMode=Tall is recommended.
    /// </summary>
    public object Footer
    {
        get => (object)GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    /// <summary>
    /// Gets or sets DisplayMode. Compact is default (32px), Tall is recommended when setting the Content or Footer.
    /// </summary>
    public DisplayMode DisplayMode
    {
        get => (DisplayMode)GetValue(DisplayModeProperty);
        set => SetValue(DisplayModeProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether gets or sets the visibility of the back button.
    /// </summary>
    public bool IsBackButtonVisible
    {
        get => (bool)GetValue(IsBackButtonVisibleProperty);
        set => SetValue(IsBackButtonVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether gets or sets the visibility of the pane toggle button.
    /// </summary>
    public bool IsPaneButtonVisible
    {
        get => (bool)GetValue(IsPaneButtonVisibleProperty);
        set => SetValue(IsPaneButtonVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets the breakpoint of when the compact state is triggered.
    /// </summary>
    public int CompactStateBreakpoint
    {
        get => (int)GetValue(CompactStateBreakpointProperty);
        set => SetValue(CompactStateBreakpointProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether gets or sets if the TitleBar should auto configure ExtendContentIntoTitleBar and CaptionButtion background colors.
    /// </summary>
    public bool AutoConfigureCustomTitleBar
    {
        get => (bool)GetValue(AutoConfigureCustomTitleBarProperty);
        set => SetValue(AutoConfigureCustomTitleBarProperty, value);
    }

    /// <summary>
    /// Gets or sets the window the TitleBar should configure (WASDK only).
    /// </summary>
    public Window Window
    {
        get => (Window)GetValue(WindowProperty);
        set => SetValue(WindowProperty, value);
    }

    private static void IsBackButtonVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((TitleBar)d).Update();
    }

    private static void IsPaneButtonVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((TitleBar)d).Update();
    }

    private static void DisplayModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((TitleBar)d).Update();
    }

    private static void ContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((TitleBar)d).Update();
    }

    private static void FooterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((TitleBar)d).Update();
    }

    private static void IconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((TitleBar)d).Update();
    }

    private static void AutoConfigureCustomTitleBarChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (((TitleBar)d).AutoConfigureCustomTitleBar)
        {
            ((TitleBar)d).Configure();
        }
        else
        {
            ((TitleBar)d).Reset();
        }
    }
}

public enum DisplayMode
{
    Standard,
    Tall,
}
