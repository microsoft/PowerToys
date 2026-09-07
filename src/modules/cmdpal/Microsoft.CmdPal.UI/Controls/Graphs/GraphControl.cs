// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using CommunityToolkit.WinUI.Controls;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Microsoft.CmdPal.UI.Controls.Graphs;

// Local rendering only. This control has no dependency on extension interfaces.
[TemplatePart(Name = HeaderPart, Type = typeof(Grid))]
[TemplatePart(Name = TitlePart, Type = typeof(TextBlock))]
[TemplatePart(Name = CaptionPart, Type = typeof(TextBlock))]
[TemplatePart(Name = LegendPart, Type = typeof(WrapPanel))]
[TemplatePart(Name = ScaleMaximumPart, Type = typeof(TextBlock))]
[TemplatePart(Name = TimeAxisPart, Type = typeof(Grid))]
[TemplatePart(Name = TimeStartPart, Type = typeof(TextBlock))]
[TemplatePart(Name = TimeEndPart, Type = typeof(TextBlock))]
public abstract partial class GraphControl : ContentControl
{
    protected const float LineStrokeWidth = 1;

    private const string HeaderPart = "PART_Header";
    private const string TitlePart = "PART_Title";
    private const string CaptionPart = "PART_Caption";
    private const string LegendPart = "PART_Legend";
    private const string ScaleMaximumPart = "PART_ScaleMaximum";
    private const string TimeAxisPart = "PART_TimeAxis";
    private const string TimeStartPart = "PART_TimeStart";
    private const string TimeEndPart = "PART_TimeEnd";

    public static readonly DependencyProperty SeriesAccentBrushProperty =
        DependencyProperty.Register(nameof(SeriesAccentBrush), typeof(Brush), typeof(GraphControl), new PropertyMetadata(null));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(GraphControl), new PropertyMetadata(string.Empty, OnTitleChanged));

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(1000d / 30) };
    private readonly UISettings _uiSettings = new();
    private Grid? _header;
    private TextBlock? _title;
    private TextBlock? _caption;
    private WrapPanel? _legend;
    private TextBlock? _scaleMaximum;
    private Grid? _timeAxis;
    private TextBlock? _timeStart;
    private TextBlock? _timeEnd;
    private LegendItem[] _legendItems = [];
    private string[] _legendLabels = [];
    private string _captionText = string.Empty;
    private string? _scaleMaximumText;
    private string? _timeStartText;
    private string? _timeEndText;
    private bool _useLineSwatches;

    private CanvasControl? _canvas;
    private XamlRoot? _attachedRoot;
    private ThemeSettings? _themeSettings;
    private bool _inViewport = true;
    private long _animationStarted;
    private bool _animate;

    protected GraphControl()
    {
        DefaultStyleKey = GetType();
        Plot = new Grid { Style = (Style)Application.Current.Resources["GraphPlotStyle"] };
        Content = Plot;
        RegisterPropertyChangedCallback(ForegroundProperty, OnThemeBrushChanged);
        RegisterPropertyChangedCallback(BorderBrushProperty, OnThemeBrushChanged);
        RegisterPropertyChangedCallback(SeriesAccentBrushProperty, OnThemeBrushChanged);

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

    public Brush? SeriesAccentBrush
    {
        get => (Brush?)GetValue(SeriesAccentBrushProperty);
        set => SetValue(SeriesAccentBrushProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    protected Grid Plot { get; }

    protected GraphSeries[] Series { get; private set; } = [];

    protected Color ForegroundColor { get; private set; }

    protected Color GridColor { get; private set; }

    protected bool HighContrast => _themeSettings?.HighContrast ?? false;

    protected virtual bool NeedsContinuousDrawing => false;

    protected bool CanAnimate => _uiSettings.AnimationsEnabled && IsLoaded;

    protected double AnimationProgress => !_animate ? 1 : Math.Clamp(Stopwatch.GetElapsedTime(_animationStarted).TotalMilliseconds / 300, 0, 1);

    protected override void OnApplyTemplate()
    {
        if (_legend is not null)
        {
            _legend.SizeChanged -= OnLegendSizeChanged;
        }

        base.OnApplyTemplate();
        _header = GetTemplateChild(HeaderPart) as Grid;
        _title = GetTemplateChild(TitlePart) as TextBlock;
        _caption = GetTemplateChild(CaptionPart) as TextBlock;
        _legend = GetTemplateChild(LegendPart) as WrapPanel;
        _scaleMaximum = GetTemplateChild(ScaleMaximumPart) as TextBlock;
        _timeAxis = GetTemplateChild(TimeAxisPart) as Grid;
        _timeStart = GetTemplateChild(TimeStartPart) as TextBlock;
        _timeEnd = GetTemplateChild(TimeEndPart) as TextBlock;
        if (_legend is not null)
        {
            _legend.SizeChanged += OnLegendSizeChanged;
        }

        RebuildLegend();
        UpdateHeader();
        UpdateTimeAxis();
        ApplyTheme();
    }

    protected static T CreateGraphElement<T>(string templateKey)
        where T : FrameworkElement
        => (T)((DataTemplate)Application.Current.Resources[templateKey]).LoadContent();

    protected void ConfigureSeries(GraphSeries[] series, bool useLineSwatches = false)
    {
        Series = (GraphSeries[])series.Clone();
        _useLineSwatches = useLineSwatches;
        _legendLabels = series.Select(series => series.Name).ToArray();
        RebuildLegend();
        Invalidate();
    }

    private void RebuildLegend()
    {
        _legendItems = [];
        if (_legend is null)
        {
            return;
        }

        _legend.Children.Clear();
        _legendItems = new LegendItem[Series.Length];
        for (var index = 0; index < Series.Length; index++)
        {
            var item = CreateGraphElement<Grid>("GraphLegendItemTemplate");
            var label = (TextBlock)item.FindName("Label");
            var swatch = (Shape)item.FindName(_useLineSwatches ? "LineSwatch" : "BlockSwatch");
            var brush = (SolidColorBrush)(_useLineSwatches ? swatch.Stroke : swatch.Fill);
            if (_useLineSwatches)
            {
                swatch.StrokeThickness = LineStrokeWidth;
                swatch.StrokeDashArray = Series[index].LineStyle switch
                {
                    GraphStrokeStyle.Dashed => new DoubleCollection { 2, 2 },
                    GraphStrokeStyle.Dotted => new DoubleCollection { 0, 2 },
                    _ => null,
                };
                if (Series[index].LineStyle == GraphStrokeStyle.Dotted)
                {
                    swatch.StrokeDashCap = PenLineCap.Round;
                }
            }

            var showSwatch = !_useLineSwatches || !Series[index].IsReadoutOnly;
            swatch.Visibility = showSwatch ? Visibility.Visible : Visibility.Collapsed;
            if (!showSwatch)
            {
                item.ColumnSpacing = 0;
            }

            if (_legend.ActualWidth > 0)
            {
                item.MaxWidth = _legend.ActualWidth;
            }

            _legend.Children.Add(item);
            _legendItems[index] = new(label, brush);
        }

        _legend.Visibility = Series.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        UpdateLegend();
        ApplyLegendTheme();
    }

    protected string SetLegend(string[] labels, string caption = "", string? accessibleCaption = null)
    {
        _legendLabels = (string[])labels.Clone();
        _captionText = caption;
        UpdateLegend();

        var summary = string.Join("   ", labels);
        accessibleCaption ??= caption;
        if (!string.IsNullOrEmpty(accessibleCaption))
        {
            summary = summary.Length == 0 ? accessibleCaption : accessibleCaption + Environment.NewLine + summary;
        }

        AutomationProperties.SetHelpText(this, summary);
        return summary;
    }

    protected void SetScaleMaximumLabel(string text)
    {
        if (_scaleMaximumText == text)
        {
            return;
        }

        _scaleMaximumText = text;
        UpdateHeader();
    }

    protected void SetTimeAxisLabels(string start, string end)
    {
        if (_timeStartText == start && _timeEndText == end)
        {
            return;
        }

        _timeStartText = start;
        _timeEndText = end;
        UpdateTimeAxis();
    }

    private void UpdateHeader()
    {
        if (_header is not null)
        {
            _header.Visibility = string.IsNullOrEmpty(Title) && _scaleMaximumText is null ? Visibility.Collapsed : Visibility.Visible;
        }

        if (_title is not null)
        {
            _title.Visibility = string.IsNullOrEmpty(Title) ? Visibility.Collapsed : Visibility.Visible;
        }

        if (_scaleMaximum is not null)
        {
            _scaleMaximum.Text = _scaleMaximumText ?? string.Empty;
            _scaleMaximum.Visibility = _scaleMaximumText is null ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private void UpdateLegend()
    {
        for (var index = 0; index < _legendItems.Length; index++)
        {
            _legendItems[index].Label.Text = _legendLabels[index];
        }

        if (_caption is not null)
        {
            _caption.Text = _captionText;
            _caption.Visibility = string.IsNullOrEmpty(_captionText) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private void UpdateTimeAxis()
    {
        if (_timeAxis is not null)
        {
            _timeAxis.Visibility = _timeStartText is null ? Visibility.Collapsed : Visibility.Visible;
        }

        if (_timeStart is not null)
        {
            _timeStart.Text = _timeStartText ?? string.Empty;
        }

        if (_timeEnd is not null)
        {
            _timeEnd.Text = _timeEndText ?? string.Empty;
        }
    }

    private void OnLegendSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (_legend is not null)
        {
            foreach (FrameworkElement item in _legend.Children)
            {
                item.MaxWidth = args.NewSize.Width;
            }
        }
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
            0 => BrushColor(SeriesAccentBrush),
            1 => light ? Color.FromArgb(255, 0, 128, 117) : Color.FromArgb(255, 83, 211, 195),
            2 => light ? Color.FromArgb(255, 171, 90, 0) : Color.FromArgb(255, 255, 185, 85),
            _ => light ? Color.FromArgb(255, 125, 67, 175) : Color.FromArgb(255, 193, 154, 255),
        };
    }

    protected Color IndicatorColor(Color? supplied)
        => HighContrast ? ForegroundColor : supplied ?? BrushColor(SeriesAccentBrush);

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

    private static Color BrushColor(Brush? brush)
        => (brush as SolidColorBrush)?.Color ?? Microsoft.UI.Colors.Gray;

    private static void OnThemeBrushChanged(DependencyObject sender, DependencyProperty property)
        => ((GraphControl)sender).ApplyTheme();

    private static void OnTitleChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        => ((GraphControl)sender).UpdateHeader();

    private void ApplyTheme()
    {
        ForegroundColor = HighContrast ? _uiSettings.UIElementColor(UIElementType.WindowText) : BrushColor(Foreground);
        GridColor = HighContrast ? ForegroundColor : BrushColor(BorderBrush);

        ApplyLegendTheme();
        Invalidate();
    }

    private void ApplyLegendTheme()
    {
        for (var index = 0; index < _legendItems.Length; index++)
        {
            _legendItems[index].Label.Foreground = Foreground;
            _legendItems[index].Brush.Color = SeriesColor(index);
        }
    }

    private readonly record struct LegendItem(TextBlock Label, SolidColorBrush Brush);

    private sealed partial class GraphAutomationPeer(GraphControl owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => owner.GetType().Name;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;
    }
}
