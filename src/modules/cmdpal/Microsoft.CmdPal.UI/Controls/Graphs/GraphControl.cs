// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Microsoft.CmdPal.UI.Controls.Graphs;

// Local rendering only. This control has no dependency on extension interfaces.
public abstract partial class GraphControl : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(1000d / 30) };
    private readonly UISettings _uiSettings = new();
    private readonly Dictionary<string, Brush> _themeBrushes = [];
    private readonly Border _border;
    private readonly TextBlock _caption = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private CanvasControl? _canvas;
    private XamlRoot? _attachedRoot;
    private ThemeSettings? _themeSettings;
    private bool _inViewport = true;
    private long _animationStarted;
    private bool _animate;

    protected GraphControl()
    {
        IsTabStop = true;
        UseSystemFocusVisuals = true;
        Plot = new Grid { MinHeight = 96 };
        var layout = new Grid { RowSpacing = 8 };
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.Children.Add(Plot);
        Grid.SetRow(_caption, 1);
        layout.Children.Add(_caption);
        _border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Child = layout,
        };
        Content = _border;
        Height = 220;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ActualThemeChanged += (_, _) => ApplyTheme();
        EffectiveViewportChanged += (_, args) =>
        {
            _inViewport = args.EffectiveViewport.Width > 0 && args.EffectiveViewport.Height > 0;
            UpdateTimer();
        };
        _timer.Tick += (_, _) =>
        {
            Invalidate();
            UpdateTimer();
        };
    }

    protected Grid Plot { get; }

    protected GraphSeries[] Series { get; private set; } = [];

    protected Color ForegroundColor { get; private set; }

    protected Color GridColor { get; private set; }

    protected bool HighContrast => _themeSettings?.HighContrast ?? false;

    protected virtual bool NeedsContinuousDrawing => false;

    protected bool CanAnimate => _uiSettings.AnimationsEnabled && IsLoaded;

    protected double AnimationProgress => !_animate ? 1 : Math.Clamp(Stopwatch.GetElapsedTime(_animationStarted).TotalMilliseconds / 300, 0, 1);

    protected void ConfigureSeries(GraphSeries[] series)
    {
        Series = (GraphSeries[])series.Clone();
        Invalidate();
    }

    protected void SetCaption(string text)
    {
        _caption.Text = text;
        AutomationProperties.SetHelpText(this, text);
    }

    protected void BeginAnimation()
    {
        _animate = CanAnimate;
        _animationStarted = Stopwatch.GetTimestamp();
        Invalidate();
        UpdateTimer();
    }

    protected void Invalidate() => _canvas?.Invalidate();

    protected Color SeriesColor(int index)
    {
        if (HighContrast)
        {
            return index % 2 == 0 ? _uiSettings.UIElementColor(UIElementType.Highlight) : ForegroundColor;
        }

        if (Series[index].Color is { } color)
        {
            return color;
        }

        var light = ActualTheme == ElementTheme.Light;
        return (index % 4) switch
        {
            0 => ResourceColor("AccentFillColorDefaultBrush"),
            1 => light ? Color.FromArgb(255, 0, 128, 117) : Color.FromArgb(255, 83, 211, 195),
            2 => light ? Color.FromArgb(255, 171, 90, 0) : Color.FromArgb(255, 255, 185, 85),
            _ => light ? Color.FromArgb(255, 125, 67, 175) : Color.FromArgb(255, 193, 154, 255),
        };
    }

    protected Color IndicatorColor(Color? supplied)
        => HighContrast ? ForegroundColor : supplied ?? ResourceColor("AccentFillColorDefaultBrush");

    protected static Color WithOpacity(Color color, double opacity)
        => Color.FromArgb((byte)(color.A * opacity), color.R, color.G, color.B);

    protected static double Ease(double progress) => progress * progress * (3 - (2 * progress));

    protected abstract void Draw(CanvasDrawingSession session, CanvasControl canvas, float width, float height);

    protected virtual void ReleaseDrawingResources()
    {
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new GraphAutomationPeer(this);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _attachedRoot = XamlRoot;

        // Desktop high-contrast notifications are scoped to the hosting window.
        _themeSettings = ThemeSettings.CreateForWindowId(_attachedRoot.ContentIslandEnvironment.AppWindowId);
        _themeSettings.Changed += OnThemeSettingsChanged;
        ApplyTheme();
        _canvas = new CanvasControl { ClearColor = Microsoft.UI.Colors.Transparent };
        _canvas.CreateResources += OnCreateResources;
        _canvas.Draw += OnDraw;
        Plot.Children.Insert(0, _canvas);
        if (_attachedRoot is not null)
        {
            _attachedRoot.Changed += OnRootChanged;
        }

        UpdateTimer();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        ReleaseDrawingResources();
        if (_themeSettings is not null)
        {
            _themeSettings.Changed -= OnThemeSettingsChanged;
            _themeSettings = null;
        }

        if (_attachedRoot is not null)
        {
            _attachedRoot.Changed -= OnRootChanged;
            _attachedRoot = null;
        }

        if (_canvas is not null)
        {
            _canvas.Draw -= OnDraw;
            _canvas.CreateResources -= OnCreateResources;
            Plot.Children.Remove(_canvas);
            _canvas.RemoveFromVisualTree();
            _canvas = null;
        }
    }

    private void OnRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => UpdateTimer();

    private void OnCreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args) => ReleaseDrawingResources();

    private void OnThemeSettingsChanged(ThemeSettings sender, object args)
        => DispatcherQueue.TryEnqueue(() =>
        {
            if (IsLoaded)
            {
                ApplyTheme();
            }
        });

    private void UpdateTimer()
    {
        if (_canvas is not null && _inViewport && (_attachedRoot?.IsHostVisible ?? false) &&
            (NeedsContinuousDrawing || AnimationProgress < 1))
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var width = (float)sender.ActualWidth;
        var height = (float)sender.ActualHeight;
        if (width > 0 && height > 0)
        {
            Draw(args.DrawingSession, sender, width, height);
        }
    }

    protected Brush ResourceBrush(string key)
    {
        if (!_themeBrushes.TryGetValue(key, out var brush))
        {
            // Application.Resources resolves against the app theme. An explicit
            // RequestedTheme makes this lookup follow the graph's window theme.
            var source = (Border)XamlReader.Load($"<Border xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" RequestedTheme=\"{ActualTheme}\" Background=\"{{ThemeResource {key}}}\" />");
            brush = source.Background;
            _themeBrushes.Add(key, brush);
        }

        return brush;
    }

    private Color ResourceColor(string key)
        => (ResourceBrush(key) as SolidColorBrush)?.Color ?? Microsoft.UI.Colors.Gray;

    private void ApplyTheme()
    {
        _themeBrushes.Clear();
        ForegroundColor = HighContrast ? _uiSettings.UIElementColor(UIElementType.WindowText) : ResourceColor("TextFillColorPrimaryBrush");
        GridColor = HighContrast ? ForegroundColor : ResourceColor("CardStrokeColorDefaultBrush");
        _border.Background = new SolidColorBrush(HighContrast ? _uiSettings.UIElementColor(UIElementType.Window) : ResourceColor("CardBackgroundFillColorDefaultBrush"));
        _border.BorderBrush = new SolidColorBrush(GridColor);
        _caption.Foreground = new SolidColorBrush(ForegroundColor);
        Invalidate();
    }

    private sealed partial class GraphAutomationPeer(GraphControl owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => owner.GetType().Name;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;
    }
}
