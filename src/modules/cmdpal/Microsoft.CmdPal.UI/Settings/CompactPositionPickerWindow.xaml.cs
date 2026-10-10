// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;
using ManagedCommon;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI.ViewManagement;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;
using WinUIEx;
using WinUIEx.Messaging;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class CompactPositionPickerWindow : WindowEx
{
    private static readonly TimeSpan BackgroundEntranceDuration = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan ControlsEntranceDuration = TimeSpan.FromMilliseconds(300);

    private const double GhostHorizontalMargin = 96;
    private const double GhostMinWidth = 280;
    private const double GhostMaxWidth = 680;
    private const double ShadowPadding = 16; // Matches CmdPalMainControl's default shadow padding.
    private const double ExpandedPreviewFadeDistance = 48;
    private const double GuidancePanelGap = 20;
    private const double GuidancePanelEdgeMargin = 24;
    private const double GuidancePanelTopClearance = 72;
    private const double EstimatedGuidancePanelHeight = 156;
    private const float DesktopBlurAmount = 6;
    private const float DesktopDimOpacity = 0.22f;
    private const string DesktopBlurProperty = "DesktopBlur.BlurAmount";

    private readonly Microsoft.UI.DisplayId _displayId;
    private readonly RectInt32 _outerBounds;
    private readonly RectInt32 _workArea;
    private readonly bool _animationsEnabled;
    private readonly bool _highContrast;
    private readonly WindowManager _windowManager;

    private CompositionBackdropBrush? _desktopBackdrop;
    private CompositionEffectBrush? _desktopBlurBrush;
    private SpriteVisual? _desktopBlurVisual;

    private uint? _dragPointerId;
    private double _dragOffsetFromCenter;
    private bool _closeRequested;
    private bool _isClosed;
    private bool _isActive;

    public event Action<double>? PositionSaved;

    public CompactPositionPickerWindow(
        DisplayArea displayArea,
        SoftwareBitmapSource? desktopScreenshot,
        double initialPercentage)
    {
        _displayId = displayArea.DisplayId;
        _outerBounds = displayArea.OuterBounds;
        _workArea = displayArea.WorkArea;
        _highContrast = new AccessibilitySettings().HighContrast;
        _animationsEnabled = new UISettings().AnimationsEnabled && !_highContrast;

        // Keep the desktop visible until the screenshot and first layout have rendered.
        SystemBackdrop = new TransparentTintBackdrop { TintColor = Colors.Transparent };

        InitializeComponent();
        PaletteGhost.Value = initialPercentage;

        ElementCompositionPreview.GetElementVisual(DesktopDimmer).Opacity = 0;
        ElementCompositionPreview.GetElementVisual(ReservedAreaOverlay).Opacity = 0;
        ElementCompositionPreview.GetElementVisual(PreviewCanvas).Opacity = 0;
        ElementCompositionPreview.GetElementVisual(CloseButton).Opacity = 0;

        DesktopScreenshot.Source = desktopScreenshot;
        AppWindow.Title = RS_.GetString("CompactPositionPickerWindow_Title");

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            // WinUIEx hides the title bar but retains the border.
            presenter.SetBorderAndTitleBar(false, false);
        }

        _windowManager = WindowManager.Get(this);
        Activated += CompactPositionPickerWindow_Activated;
        Closed += CompactPositionPickerWindow_Closed;
        AppWindow.MoveAndResize(_outerBounds);

        // Monitor display changes after the initial placement.
        _windowManager.WindowMessageReceived += WindowManager_WindowMessageReceived;
    }

    private void WindowManager_WindowMessageReceived(object? sender, WindowMessageEventArgs e)
    {
        if (!_closeRequested && !_isClosed && Root.IsLoaded && e.Message.MessageId == PInvoke.WM_SETFOCUS)
        {
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                if (!_closeRequested && !_isClosed && _isActive && Root.IsLoaded)
                {
                    var focusedControl = FocusManager.GetFocusedElement(Root.XamlRoot) as Control ?? PaletteGhost;
                    _ = focusedControl.Focus(FocusState.Programmatic);
                }
            });
        }

        if (e.Message.MessageId == PInvoke.WM_DPICHANGED
            || ((e.Message.MessageId == PInvoke.WM_DISPLAYCHANGE
                    || (e.Message.MessageId == PInvoke.WM_SETTINGCHANGE
                        && e.Message.WParam.ToUInt64() == (uint)SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETWORKAREA))
                && HasDisplayGeometryChanged()))
        {
            RequestClose();
        }
    }

    private void CompactPositionPickerWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        var wasActive = _isActive;
        _isActive = args.WindowActivationState != WindowActivationState.Deactivated;
        if (wasActive && !_isActive)
        {
            RequestClose();
        }
    }

    private void RequestClose()
    {
        if (!_closeRequested && !_isClosed)
        {
            // Close after the current native message has finished processing.
            _closeRequested = DispatcherQueue.TryEnqueue(() =>
            {
                if (!_isClosed)
                {
                    Close();
                }
            });
        }
    }

    private bool HasDisplayGeometryChanged()
    {
        var displayArea = DisplayArea.GetFromDisplayId(_displayId);
        return displayArea is null
            || displayArea.OuterBounds != _outerBounds
            || displayArea.WorkArea != _workArea;
    }

    private void CompactPositionPickerWindow_Closed(object sender, WindowEventArgs args)
    {
        _isClosed = true;
        _isActive = false;
        Activated -= CompactPositionPickerWindow_Activated;
        Closed -= CompactPositionPickerWindow_Closed;
        _windowManager.WindowMessageReceived -= WindowManager_WindowMessageReceived;
        PositionSaved = null;
        CompositionTarget.Rendered -= FirstFrame_Rendered;
        ElementCompositionPreview.SetElementChildVisual(DesktopBlurHost, null);
        _desktopBlurVisual?.Dispose();
        _desktopBlurBrush?.Dispose();
        _desktopBackdrop?.Dispose();
        var screenshot = DesktopScreenshot.Source as SoftwareBitmapSource;
        DesktopScreenshot.Source = null;
        screenshot?.Dispose();
    }

    private void Root_Loaded(object sender, RoutedEventArgs e)
    {
        Root.Loaded -= Root_Loaded;
        var instructionText = GetInstructionText(InstructionText.Text, KeyboardInstructionText.Text);
        AutomationProperties.SetHelpText(Root, instructionText);
        AutomationProperties.SetHelpText(PaletteGhost, instructionText);
        AutomationProperties.SetFullDescription(PaletteGhost, ExpandedPreviewHint.Text);

        UpdateReservedAreaHatching();
        UpdatePositionRange();
        UpdateGhostPosition();
        InitializeDesktopBlur();
        CompositionTarget.Rendered += FirstFrame_Rendered;
    }

    private void Root_LosingFocus(UIElement sender, LosingFocusEventArgs args)
    {
        if (args.NewFocusedElement is null)
        {
            return;
        }

        // Keep background clicks from moving focus outside the picker's key-event route.
        for (DependencyObject? ancestor = Root; ancestor is not null; ancestor = VisualTreeHelper.GetParent(ancestor))
        {
            if (ReferenceEquals(args.NewFocusedElement, ancestor))
            {
                _ = args.TryCancel();
                return;
            }
        }
    }

    private void InitializeDesktopBlur()
    {
        if (_highContrast || DesktopScreenshot.Source is null)
        {
            return;
        }

        try
        {
            var compositor = ElementCompositionPreview.GetElementVisual(DesktopBlurHost).Compositor;
            using var blur = new GaussianBlurEffect
            {
                Name = "DesktopBlur",
                BlurAmount = 0,
                BorderMode = EffectBorderMode.Hard,
                Optimization = EffectOptimization.Balanced,
                Source = new CompositionEffectSourceParameter("Desktop"),
            };
            using var factory = compositor.CreateEffectFactory(blur, new[] { DesktopBlurProperty });
            _desktopBackdrop = compositor.CreateBackdropBrush();
            _desktopBlurBrush = factory.CreateBrush();
            _desktopBlurBrush.SetSourceParameter("Desktop", _desktopBackdrop);
            _desktopBlurVisual = compositor.CreateSpriteVisual();
            _desktopBlurVisual.RelativeSizeAdjustment = Vector2.One;
            _desktopBlurVisual.Brush = _desktopBlurBrush;
            ElementCompositionPreview.SetElementChildVisual(DesktopBlurHost, _desktopBlurVisual);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to initialize the compact position picker blur", ex);
        }
    }

    private void FirstFrame_Rendered(object? sender, RenderedEventArgs args)
    {
        CompositionTarget.Rendered -= FirstFrame_Rendered;

        if (_desktopBlurBrush is { } blurBrush)
        {
            blurBrush.Properties.InsertScalar(DesktopBlurProperty, DesktopBlurAmount);
            if (_animationsEnabled)
            {
                using var blurAnimation = CreateEntranceAnimation(blurBrush.Compositor, DesktopBlurAmount, BackgroundEntranceDuration);
                blurBrush.StartAnimation(DesktopBlurProperty, blurAnimation);
            }
        }

        Reveal(DesktopDimmer, DesktopDimOpacity, BackgroundEntranceDuration);
        Reveal(ReservedAreaOverlay);
        Reveal(PreviewCanvas);
        Reveal(CloseButton);
        _ = PaletteGhost.Focus(FocusState.Programmatic);
    }

    private void Reveal(UIElement element, float opacity = 1, TimeSpan? duration = null)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.Opacity = opacity;
        if (_animationsEnabled)
        {
            using var animation = CreateEntranceAnimation(visual.Compositor, opacity, duration ?? ControlsEntranceDuration);
            visual.StartAnimation(nameof(Visual.Opacity), animation);
        }
    }

    private static ScalarKeyFrameAnimation CreateEntranceAnimation(Compositor compositor, float target, TimeSpan duration)
    {
        var animation = compositor.CreateScalarKeyFrameAnimation();
        animation.Duration = duration;
        animation.InsertKeyFrame(0, 0);
        using var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.42f, 0), new Vector2(0.58f, 1));
        animation.InsertKeyFrame(1, target, easing);
        return animation;
    }

    private void Root_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateReservedAreaHatching();
        UpdatePositionRange();
        UpdateGhostPosition();
    }

    private void Root_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!e.Handled)
        {
            e.Handled = PaletteGhost.HandleKey(e.Key);
        }
    }

    private void UpdateReservedAreaHatching()
    {
        var width = Root.ActualWidth;
        var height = Root.ActualHeight;
        if (width <= 0 || height <= 0 || _outerBounds.Width <= 0 || _outerBounds.Height <= 0)
        {
            return;
        }

        var scaleX = width / _outerBounds.Width;
        var scaleY = height / _outerBounds.Height;
        var left = Math.Clamp((_workArea.X - _outerBounds.X) * scaleX, 0, width);
        var top = Math.Clamp((_workArea.Y - _outerBounds.Y) * scaleY, 0, height);
        var right = Math.Clamp((_workArea.X - _outerBounds.X + _workArea.Width) * scaleX, left, width);
        var bottom = Math.Clamp((_workArea.Y - _outerBounds.Y + _workArea.Height) * scaleY, top, height);
        var areas = new GeometryGroup();
        var strokes = new PathGeometry();
        var random = new Random(0);

        AddArea(new Rect(0, 0, width, top));
        AddArea(new Rect(0, bottom, width, height - bottom));
        AddArea(new Rect(0, top, left, bottom - top));
        AddArea(new Rect(right, top, width - right, bottom - top));

        ReservedAreaFill.Data = areas;
        ReservedAreaFill.Opacity = _highContrast ? 1 : 0.2;
        ReservedAreaHatching.Data = strokes;
        ReservedAreaHatching.Opacity = _highContrast ? 1 : 0.35;
        ReservedAreaHint.Visibility = areas.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        void AddArea(Rect area)
        {
            if (area.Width <= 0 || area.Height <= 0)
            {
                return;
            }

            areas.Children.Add(new RectangleGeometry { Rect = area });
            for (var x = -area.Height; x < area.Width; x += 10 + (random.NextDouble() * 4))
            {
                var startY = Math.Max(0, -x);
                var endY = Math.Min(area.Height, area.Width - x);
                var start = new Point(area.Left + x + startY, area.Top + startY);
                var end = new Point(area.Left + x + endY, area.Top + endY);
                var length = endY - startY;
                var figure = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };

                // Small, repeatable bends keep the hatch looking hand drawn without flickering.
                figure.Segments.Add(new BezierSegment
                {
                    Point1 = new Point(start.X + (length / 3), Math.Clamp(start.Y + (length / 3) + ((random.NextDouble() - 0.5) * 3), area.Top, area.Bottom)),
                    Point2 = new Point(end.X - (length / 3), Math.Clamp(end.Y - (length / 3) + ((random.NextDouble() - 0.5) * 3), area.Top, area.Bottom)),
                    Point3 = end,
                });
                strokes.Figures.Add(figure);
            }
        }
    }

    private void GuidancePanel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateGhostPosition();
    }

    private void Root_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var properties = e.GetCurrentPoint(Root).Properties;
        e.Handled = PaletteGhost.HandleMouseWheel(properties.MouseWheelDelta, properties.IsHorizontalMouseWheel);
    }

    private void PaletteGhost_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pointerPoint = e.GetCurrentPoint(Root);
        if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && !pointerPoint.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _dragPointerId = e.Pointer.PointerId;
        _ = PaletteGhost.Focus(FocusState.Pointer);
        _dragOffsetFromCenter = pointerPoint.Position.Y - GetGhostCenterY();
        _ = PaletteGhost.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void PaletteGhost_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragPointerId != e.Pointer.PointerId)
        {
            return;
        }

        var pointerY = e.GetCurrentPoint(Root).Position.Y;
        SetPositionFromCenterY(pointerY - _dragOffsetFromCenter);
        e.Handled = true;
    }

    private void PaletteGhost_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        EndDrag(e);
    }

    private void PaletteGhost_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        EndDrag(e);
    }

    private void PaletteGhost_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (_dragPointerId == e.Pointer.PointerId)
        {
            _dragPointerId = null;
        }
    }

    private void EndDrag(PointerRoutedEventArgs e)
    {
        if (_dragPointerId != e.Pointer.PointerId)
        {
            return;
        }

        PaletteGhost.ReleasePointerCapture(e.Pointer);
        _dragPointerId = null;
        e.Handled = true;
    }

    private void DefaultButton_Click(object sender, RoutedEventArgs e)
    {
        SetPosition(SettingsModel.DefaultCompactCenterHeightPercentage);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        PositionSaved?.Invoke(Math.Round(PaletteGhost.Value, MidpointRounding.AwayFromZero));
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static string GetInstructionText(string instruction, string keyboardInstruction) => $"{instruction}\n{keyboardInstruction}";

    private void SetPosition(double percentage)
    {
        PaletteGhost.Value = Math.Clamp(percentage, PaletteGhost.Minimum, PaletteGhost.Maximum);
    }

    private void PaletteGhost_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        UpdateGhostPosition();
    }

    private void SetPositionFromCenterY(double centerY)
    {
        var (workAreaTop, workAreaHeight) = GetWorkAreaMetrics();
        if (workAreaHeight <= 0)
        {
            return;
        }

        var fractionFromTop = (centerY - workAreaTop) / workAreaHeight;
        SetPosition((1 - fractionFromTop) * 100);
    }

    private void UpdatePositionRange()
    {
        var (_, workAreaHeight) = GetWorkAreaMetrics();
        if (workAreaHeight <= 0)
        {
            return;
        }

        var maximum = Math.Max(0, (1 - (((PaletteGhost.Height / 2) + ShadowPadding) / workAreaHeight)) * 100);
        PaletteGhost.Minimum = Math.Min(maximum, CompactPositionConstraints.GetMinimumPercentageFromBottom(workAreaHeight));
        PaletteGhost.Maximum = maximum;
    }

    private void UpdateGhostPosition()
    {
        if (Root.ActualWidth <= 0 || Root.ActualHeight <= 0)
        {
            return;
        }

        PaletteGhost.Width = Math.Clamp(
            Root.ActualWidth - (GhostHorizontalMargin * 2),
            GhostMinWidth,
            GhostMaxWidth);

        var (workAreaTop, workAreaHeight) = GetWorkAreaMetrics();
        var ghostCenterY = workAreaTop + (workAreaHeight * (1 - (PaletteGhost.Value / 100)));
        var ghostLeft = (Root.ActualWidth - PaletteGhost.Width) / 2;
        Canvas.SetLeft(PaletteGhost, ghostLeft);
        Canvas.SetTop(PaletteGhost, ghostCenterY - (PaletteGhost.Height / 2));

        var expansionProgress = UpdateExpandedPreview(ghostLeft, ghostCenterY, workAreaTop + workAreaHeight, workAreaHeight);
        UpdateGuidancePanelPosition(ghostLeft, ghostCenterY, expansionProgress);
    }

    private double UpdateExpandedPreview(double ghostLeft, double ghostCenterY, double workAreaBottom, double workAreaHeight)
    {
        var reservedSpace = workAreaHeight * PaletteGhost.Minimum / 100;
        var distanceToLimit = workAreaBottom - ghostCenterY - reservedSpace;
        var opacity = Math.Clamp(1 - (distanceToLimit / ExpandedPreviewFadeDistance), 0, 1);
        ExpandedPreview.Visibility = opacity > 0 ? Visibility.Visible : Visibility.Collapsed;
        ExpandedPreview.Opacity = _highContrast ? 1 : opacity;
        ExpandedPreview.Width = PaletteGhost.Width;
        ExpandedPreview.Height = Math.Max(PaletteGhost.Height, (PaletteGhost.Height / 2) + Math.Min(reservedSpace, workAreaBottom - ghostCenterY) - ShadowPadding);
        Canvas.SetLeft(ExpandedPreview, ghostLeft);
        Canvas.SetTop(ExpandedPreview, Canvas.GetTop(PaletteGhost));

        var expandedOpacity = ExpandedPreview.Visibility == Visibility.Visible ? ExpandedPreview.Opacity : 0;
        UpdateShadowArea(CompactShadowArea, PaletteGhost, 1 - expandedOpacity);
        UpdateShadowArea(ExpandedShadowArea, ExpandedPreview, expandedOpacity);
        return opacity;
    }

    private void UpdateShadowArea(FrameworkElement shadowArea, FrameworkElement preview, double opacity)
    {
        shadowArea.Width = preview.Width + (ShadowPadding * 2);
        shadowArea.Height = preview.Height + (ShadowPadding * 2);
        shadowArea.Opacity = opacity * (_highContrast ? 1 : 0.22);
        Canvas.SetLeft(shadowArea, Canvas.GetLeft(preview) - ShadowPadding);
        Canvas.SetTop(shadowArea, Canvas.GetTop(preview) - ShadowPadding);
    }

    private void UpdateGuidancePanelPosition(double ghostLeft, double ghostCenterY, double expansionProgress)
    {
        var panelWidth = GuidancePanel.ActualWidth > 0 ? GuidancePanel.ActualWidth : GuidancePanel.Width;
        var panelHeight = GuidancePanel.ActualHeight > 0 ? GuidancePanel.ActualHeight : EstimatedGuidancePanelHeight;
        var ghostRight = ghostLeft + PaletteGhost.Width;
        var rightPanelLeft = ghostRight + GuidancePanelGap;
        var leftPanelLeft = ghostLeft - GuidancePanelGap - panelWidth;
        var maximumPanelLeft = Math.Max(
            GuidancePanelEdgeMargin,
            Root.ActualWidth - panelWidth - GuidancePanelEdgeMargin);

        double panelLeft;
        var fitsBesideGhost = true;
        if (rightPanelLeft <= maximumPanelLeft)
        {
            panelLeft = rightPanelLeft;
        }
        else if (leftPanelLeft >= GuidancePanelEdgeMargin)
        {
            panelLeft = leftPanelLeft;
        }
        else
        {
            fitsBesideGhost = false;
            panelLeft = Math.Clamp(
                (Root.ActualWidth - panelWidth) / 2,
                GuidancePanelEdgeMargin,
                maximumPanelLeft);
        }

        var maximumPanelTop = Math.Max(
            GuidancePanelEdgeMargin,
            Root.ActualHeight - panelHeight - GuidancePanelEdgeMargin);
        var minimumPanelTop = Math.Min(GuidancePanelTopClearance, maximumPanelTop);
        var panelTop = ghostCenterY - (panelHeight / 2);

        if (!fitsBesideGhost)
        {
            var ghostTop = Canvas.GetTop(PaletteGhost);

            // Reserve the full height before it appears in high contrast to avoid a layout jump.
            var previewHeight = _highContrast
                ? ExpandedPreview.Height
                : PaletteGhost.Height + ((ExpandedPreview.Height - PaletteGhost.Height) * expansionProgress);
            var belowPreview = ghostTop + previewHeight + GuidancePanelGap;
            var aboveGhost = Math.Max(ghostTop - GuidancePanelGap - panelHeight, minimumPanelTop);
            if (belowPreview <= maximumPanelTop)
            {
                panelTop = belowPreview;
            }
            else if (aboveGhost + panelHeight <= ghostTop)
            {
                panelTop = aboveGhost;
            }
            else
            {
                // Cover the results preview only when neither clear placement fits.
                panelTop = ghostTop + PaletteGhost.Height + GuidancePanelGap;
            }
        }

        Canvas.SetLeft(GuidancePanel, panelLeft);
        Canvas.SetTop(GuidancePanel, Math.Clamp(panelTop, minimumPanelTop, maximumPanelTop));
    }

    private double GetGhostCenterY()
    {
        return Canvas.GetTop(PaletteGhost) + (PaletteGhost.Height / 2);
    }

    private (double Top, double Height) GetWorkAreaMetrics()
    {
        if (_outerBounds.Height <= 0)
        {
            return (0, Root.ActualHeight);
        }

        var scaleY = Root.ActualHeight / _outerBounds.Height;
        var top = (_workArea.Y - _outerBounds.Y) * scaleY;
        return (top, _workArea.Height * scaleY);
    }
}
