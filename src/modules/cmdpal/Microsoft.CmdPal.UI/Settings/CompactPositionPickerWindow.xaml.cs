// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Numerics;
using System.Text;
using ManagedCommon;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.System;
using Windows.UI.ViewManagement;
using WinUIEx;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class CompactPositionPickerWindow : WindowEx
{
    private static readonly CompositeFormat PositionFormat = CompositeFormat.Parse(RS_.GetString("CompactPositionPickerWindow_PositionFormat"));
    private static readonly TimeSpan BackgroundEntranceDuration = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan ControlsEntranceDuration = TimeSpan.FromMilliseconds(300);

    private const double WheelStep = 5;
    private const double KeyboardStep = 1;
    private const double KeyboardLargeStep = 5;
    private const double GhostHorizontalMargin = 48;
    private const double GhostMinWidth = 280;
    private const double GhostMaxWidth = 680;
    private const double GuidancePanelGap = 20;
    private const double GuidancePanelEdgeMargin = 24;
    private const double GuidancePanelTopClearance = 72;
    private const double EstimatedGuidancePanelHeight = 156;
    private const float DesktopBlurAmount = 6;
    private const float DesktopDimOpacity = 0.22f;
    private const string DesktopBlurProperty = "DesktopBlur.BlurAmount";

    private readonly RectInt32 _outerBounds;
    private readonly RectInt32 _workArea;
    private readonly bool _animationsEnabled;
    private readonly bool _highContrast;

    private CompositionBackdropBrush? _desktopBackdrop;
    private CompositionEffectBrush? _desktopBlurBrush;
    private SpriteVisual? _desktopBlurVisual;

    private double _selectedPercentage;
    private uint? _dragPointerId;
    private double _dragOffsetFromCenter;

    public event Action<double>? PositionSaved;

    public CompactPositionPickerWindow(
        DisplayArea displayArea,
        ImageSource? desktopScreenshot,
        double initialPercentage)
    {
        _outerBounds = displayArea.OuterBounds;
        _workArea = displayArea.WorkArea;
        _selectedPercentage = initialPercentage;
        _highContrast = new AccessibilitySettings().HighContrast;
        _animationsEnabled = new UISettings().AnimationsEnabled && !_highContrast;

        // Keep the desktop visible until the screenshot and first layout have rendered.
        SystemBackdrop = new TransparentTintBackdrop { TintColor = Colors.Transparent };

        InitializeComponent();

        ElementCompositionPreview.GetElementVisual(DesktopDimmer).Opacity = 0;
        ElementCompositionPreview.GetElementVisual(PreviewCanvas).Opacity = 0;
        ElementCompositionPreview.GetElementVisual(CloseButton).Opacity = 0;

        DesktopScreenshot.Source = desktopScreenshot;
        AppWindow.Title = RS_.GetString("CompactPositionPickerWindow_Title");

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        AppWindow.MoveAndResize(_outerBounds);
        Closed += CompactPositionPickerWindow_Closed;
    }

    private void CompactPositionPickerWindow_Closed(object sender, WindowEventArgs args)
    {
        CompositionTarget.Rendered -= FirstFrame_Rendered;
        ElementCompositionPreview.SetElementChildVisual(DesktopBlurHost, null);
        _desktopBlurVisual?.Dispose();
        _desktopBlurBrush?.Dispose();
        _desktopBackdrop?.Dispose();
        DesktopScreenshot.Source = null;
    }

    private void Root_Loaded(object sender, RoutedEventArgs e)
    {
        Root.Loaded -= Root_Loaded;
        UpdateGhostPosition();
        InitializeDesktopBlur();
        CompositionTarget.Rendered += FirstFrame_Rendered;
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
            using var factory = compositor.CreateEffectFactory(blur, [DesktopBlurProperty]);
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
        Reveal(PreviewCanvas);
        Reveal(CloseButton);
        _ = Root.Focus(FocusState.Programmatic);
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
        UpdateGhostPosition();
    }

    private void GuidancePanel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateGhostPosition();
    }

    private void Root_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(Root).Properties.MouseWheelDelta;
        if (delta == 0)
        {
            return;
        }

        ChangePosition(delta > 0 ? WheelStep : -WheelStep);
        e.Handled = true;
    }

    private void Root_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Up:
                ChangePosition(KeyboardStep);
                e.Handled = true;
                break;

            case VirtualKey.Down:
                ChangePosition(-KeyboardStep);
                e.Handled = true;
                break;

            case VirtualKey.PageUp:
                ChangePosition(KeyboardLargeStep);
                e.Handled = true;
                break;

            case VirtualKey.PageDown:
                ChangePosition(-KeyboardLargeStep);
                e.Handled = true;
                break;

            case VirtualKey.Home:
                SetPosition(100);
                e.Handled = true;
                break;

            case VirtualKey.End:
                SetPosition(0);
                e.Handled = true;
                break;
        }
    }

    private void PaletteGhost_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pointerPoint = e.GetCurrentPoint(Root);
        if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && !pointerPoint.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _dragPointerId = e.Pointer.PointerId;
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

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        PositionSaved?.Invoke(Math.Round(_selectedPercentage, MidpointRounding.AwayFromZero));
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

    private void ChangePosition(double delta)
    {
        SetPosition(_selectedPercentage + delta);
    }

    private void SetPosition(double percentage)
    {
        var (_, workAreaHeight) = GetWorkAreaMetrics();
        _selectedPercentage = CompactPositionConstraints.ClampPercentageFromBottom(
            percentage,
            workAreaHeight);
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
        _selectedPercentage = CompactPositionConstraints.ClampPercentageFromBottom(
            _selectedPercentage,
            workAreaHeight);
        var ghostCenterY = workAreaTop + (workAreaHeight * (1 - (_selectedPercentage / 100)));
        var ghostLeft = (Root.ActualWidth - PaletteGhost.Width) / 2;
        Canvas.SetLeft(PaletteGhost, ghostLeft);
        Canvas.SetTop(PaletteGhost, ghostCenterY - (PaletteGhost.Height / 2));

        var positionText = string.Format(
            CultureInfo.CurrentCulture,
            PositionFormat,
            _selectedPercentage);
        PositionValueText.Text = positionText;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(PaletteGhost, positionText);
        UpdateGuidancePanelPosition(ghostLeft, ghostCenterY);
    }

    private void UpdateGuidancePanelPosition(double ghostLeft, double ghostCenterY)
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
            var belowGhost = Canvas.GetTop(PaletteGhost) + PaletteGhost.Height + GuidancePanelGap;
            var aboveGhost = Canvas.GetTop(PaletteGhost) - GuidancePanelGap - panelHeight;
            panelTop = belowGhost <= maximumPanelTop ? belowGhost : aboveGhost;
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
