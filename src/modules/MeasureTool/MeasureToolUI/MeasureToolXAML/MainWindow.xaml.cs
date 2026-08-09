// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

using ManagedCommon;
using Microsoft.PowerToys.Common.UI.Controls;
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Settings.UI.Library.Enumerations;
using Windows.Foundation;
using Windows.Graphics;
using WinUIEx;

namespace MeasureToolUI
{
    /// <summary>
    /// The Screen Ruler toolbar. A shared <see cref="TransparentWindow"/> hosting a
    /// TransientSurface "card" (border/corner-radius/acrylic/shadow chrome), draggable via a
    /// dedicated 32x32 grip, and anchored on the monitor under the mouse cursor to the
    /// <see cref="MeasureToolToolbarPosition"/> configured in Settings every time the process is
    /// summoned. A manual drag only repositions the current visible toolbar (see
    /// <see cref="OnAppWindowChanged"/>); AppWindow.Position is never written back to settings, so
    /// the next summon (a fresh process - see
    /// MeasureToolModuleInterface's launch/terminate hotkey handling) always restores the configured
    /// anchor on whichever monitor contains the cursor at that time.
    /// </summary>
    public sealed partial class MainWindow : TransparentWindow, IDisposable
    {
        // Gap kept between the visible surface and the work-area edge for edge/corner anchors.
        private const double AnchorInsetDip = 24;

        private const uint WmSysCommand = 0x0112;
        private const uint WmNcHitTest = 0x0084;
        private const uint WmNcCalcSize = 0x0083;
        private const uint WmNcActivate = 0x0086;
        private const uint WmNcLeftButtonDoubleClick = 0x00A3;
        private const nuint ScMove = 0xF010;
        private const nuint HtCaption = 0x0002;
        private const int HtTransparent = -1;
        private const int GwlWndProc = -4;

        private readonly Settings settings = new();
        private readonly nint _hwnd;
        private readonly WindowProcDelegate _windowProc;

        private PowerToys.MeasureToolCore.Core _coreLogic;
        private XamlRoot _subscribedXamlRoot;
        private nint _originalWndProc;
        private MeasureToolMeasureStyle _selectedSpacingStyle = MeasureToolMeasureStyle.Spacing;
        private double _toolbarWidthDip;
        private double _toolbarHeightDip;
        private bool? _restartSpacingAfterFlyoutClose;
        private bool _hasShownToolbar;
        private bool _toolbarVisible;
        private bool _shuttingDown;
        private bool _layoutRefreshQueued;
        private bool _disposed;

        public MainWindow(PowerToys.MeasureToolCore.Core core)
        {
            InitializeComponent();

            _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);

            // CmdPal's outer HWND paints nothing; its material is confined to the inset
            // SystemBackdropElement. TransparentTintBackdrop's default tint is not guaranteed
            // to be transparent, so set it explicitly instead of tinting the shadow padding.
            ApplyPseudoWindowHostChrome();

            _windowProc = WindowProc;
            _originalWndProc = SetWindowLongPtr(
                _hwnd,
                GwlWndProc,
                Marshal.GetFunctionPointerForDelegate(_windowProc));

            this.SetIsAlwaysOnTop(true);

            try
            {
                this.SetIsShownInSwitchers(false);
            }
            catch (NotImplementedException)
            {
                // WinUI will throw if explorer is not running, safely ignore
            }
            catch (Exception)
            {
            }

            // Wire the card to this window's Show/Hide so the shared TransientSurface owns the
            // directional reveal and TransparentWindow's SW_SHOWNA sequencing remains centralized.
            Surface.SubscribeTo(this);

            _coreLogic = core;
            _coreLogic.InitResources();
            _coreLogic.SetGuidePresenceChangedEvent(new PowerToys.MeasureToolCore.GuidePresenceChanged(OnGuidePresenceChanged));
            _coreLogic.SetToolbarWindowHandle((long)_hwnd);
            Closed += MainWindow_Closed;
            Surface.Loaded += OnSurfaceLoaded;
            Surface.Unloaded += OnSurfaceUnloaded;
            Surface.SizeChanged += OnSurfaceSizeChanged;
            Showing += OnShowing;

            var positionSetting = MeasureToolToolbarPlacement.Normalize(settings.ToolbarPosition);
            SizeToolbarHost();
            ConfigureTransitions(positionSetting);

            // Resolve the monitor containing the mouse cursor (FancyZones "active monitor"
            // semantics - GetCursorPos + DisplayArea.GetFromPoint), falling back to the primary/
            // current display if that ever fails (e.g. no cursor position available).
            if (!FlyoutWindowHelper.TryGetDisplayAreaAtCursor(out var displayArea) || displayArea is null)
            {
                displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
            }

            PositionOnDisplay(displayArea, positionSetting);

            AppWindow.Changed += OnAppWindowChanged;
        }

        /// <summary>
        /// Resolves the configured anchor for <paramref name="positionSetting"/> on
        /// <paramref name="displayArea"/>'s work area, then moves/resizes this window there (via the
        /// cross-monitor/mixed-DPI-safe <see cref="FlyoutWindowHelper.MoveAndResizeOnDisplay"/>) and
        /// pushes the resulting visual bounds to the native measurement overlays so they start in
        /// sync with the toolbar's first frame - all before the window is ever shown, so there is no
        /// visible reposition/resize flash once it is.
        /// </summary>
        private void PositionOnDisplay(DisplayArea displayArea, MeasureToolToolbarPosition positionSetting)
        {
            double dpiScale = FlyoutWindowHelper.GetDpiScale(displayArea);
            var work = displayArea.WorkArea;

            var (surfaceX, surfaceY) = MeasureToolToolbarPlacement.GetAnchorPosition(
                positionSetting,
                work.X,
                work.Y,
                work.Width,
                work.Height,
                _toolbarWidthDip,
                _toolbarHeightDip,
                AnchorInsetDip,
                dpiScale);

            var (windowWidthPx, windowHeightPx, leftMarginPx, topMarginPx) = GetToolbarHostSize(dpiScale);

            var windowRect = new RectInt32(
                surfaceX - leftMarginPx,
                surfaceY - topMarginPx,
                windowWidthPx,
                windowHeightPx);
            FlyoutWindowHelper.MoveAndResizeOnDisplay(this, displayArea, windowRect);
            SetOverlayExclusionBounds(windowRect);
        }

        private void SizeToolbarHost()
        {
            ContentRoot.InvalidateMeasure();
            ContentRoot.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            _toolbarWidthDip = Math.Ceiling(ContentRoot.DesiredSize.Width);
            _toolbarHeightDip = Math.Ceiling(ContentRoot.DesiredSize.Height);

            if (_toolbarWidthDip <= 0 || _toolbarHeightDip <= 0)
            {
                throw new InvalidOperationException("Screen Ruler toolbar content must have a nonzero desired size.");
            }

            double horizontalShadowPaddingDip = Surface.Padding.Left + Surface.Padding.Right;
            double verticalShadowPaddingDip = Surface.Padding.Top + Surface.Padding.Bottom;
            Surface.Width = _toolbarWidthDip + horizontalShadowPaddingDip;
            Surface.Height = _toolbarHeightDip + verticalShadowPaddingDip;
        }

        private (int Width, int Height, int LeftMargin, int TopMargin) GetToolbarHostSize(double dpiScale)
        {
            int contentWidthPx = FlyoutWindowHelper.ScaleToPhysicalPixels((int)_toolbarWidthDip, dpiScale);
            int contentHeightPx = FlyoutWindowHelper.ScaleToPhysicalPixels((int)_toolbarHeightDip, dpiScale);
            var shadowPadding = Surface.Padding;
            int leftMarginPx = FlyoutWindowHelper.ScaleToPhysicalPixels((int)shadowPadding.Left, dpiScale);
            int topMarginPx = FlyoutWindowHelper.ScaleToPhysicalPixels((int)shadowPadding.Top, dpiScale);
            int rightMarginPx = FlyoutWindowHelper.ScaleToPhysicalPixels((int)shadowPadding.Right, dpiScale);
            int bottomMarginPx = FlyoutWindowHelper.ScaleToPhysicalPixels((int)shadowPadding.Bottom, dpiScale);

            return (
                contentWidthPx + leftMarginPx + rightMarginPx,
                contentHeightPx + topMarginPx + bottomMarginPx,
                leftMarginPx,
                topMarginPx);
        }

        private void OnGuidePresenceChanged(bool hasGuides)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_disposed || _shuttingDown)
                {
                    return;
                }

                if (!hasGuides && _hasShownToolbar && !_toolbarVisible)
                {
                    Shutdown();
                    return;
                }

                UpdateClearGuidesButtonVisibility(hasGuides);
            });
        }

        private void UpdateClearGuidesButtonVisibility(bool hasGuides)
        {
            Visibility visibility = hasGuides ? Visibility.Visible : Visibility.Collapsed;
            if (btnClearGuides.Visibility == visibility)
            {
                return;
            }

            btnClearGuides.Visibility = visibility;
            ResizeToolbarForContentChange();
        }

        private void ResizeToolbarForContentChange()
        {
            SizeToolbarHost();
            if (!_hasShownToolbar || _disposed)
            {
                return;
            }

            DisplayArea displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
            double dpiScale = FlyoutWindowHelper.GetDpiScale(displayArea);
            var (widthPx, heightPx, _, _) = GetToolbarHostSize(dpiScale);
            var workArea = displayArea.WorkArea;
            int maxX = workArea.X + workArea.Width - widthPx;
            int maxY = workArea.Y + workArea.Height - heightPx;
            int x = maxX < workArea.X ? workArea.X : Math.Clamp(AppWindow.Position.X, workArea.X, maxX);
            int y = maxY < workArea.Y ? workArea.Y : Math.Clamp(AppWindow.Position.Y, workArea.Y, maxY);
            var windowRect = new RectInt32(x, y, widthPx, heightPx);

            FlyoutWindowHelper.MoveAndResizeOnDisplay(this, displayArea, windowRect);
            SetOverlayExclusionBounds(windowRect);
            QueueLayoutDependentRefresh();
        }

        private void ConfigureTransitions(MeasureToolToolbarPosition position)
        {
            Transition transition = position switch
            {
                MeasureToolToolbarPosition.TopLeft or
                MeasureToolToolbarPosition.TopCenter or
                MeasureToolToolbarPosition.TopRight => Transition.Top,
                MeasureToolToolbarPosition.BottomLeft or
                MeasureToolToolbarPosition.BottomCenter or
                MeasureToolToolbarPosition.BottomRight => Transition.Bottom,
                _ => Transition.Pop,
            };

            Surface.ShowTransition = transition;
            Surface.HideTransition = transition;
        }

        private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidPositionChange || args.DidSizeChange)
            {
                // Live-sync while dragging: pushed on every native move/size notification, which is
                // already coalesced to the OS's own move-message cadence, and (since the last
                // notification before the mouse button is released carries the settled position)
                // this also serves as the final update - there is no separate "drag ended" event to
                // wait for with a native caption-region move loop.
                UpdateOverlayExclusionBounds();
            }

            if (args.DidSizeChange)
            {
                // A DPI change while dragging across monitors can reset chrome before XAML has
                // completed its corresponding layout pass. Re-apply chrome now, but defer region
                // math to Loaded/SizeChanged/XamlRoot.Changed (and a low-priority dispatcher turn).
                ApplyPseudoWindowHostChrome();
                QueueLayoutDependentRefresh();
            }
        }

        public void ShowToolbar()
        {
            if (_disposed || _shuttingDown || _toolbarVisible)
            {
                return;
            }

            var positionSetting = MeasureToolToolbarPlacement.Normalize(settings.ToolbarPosition);
            UpdateClearGuidesButtonVisibility(_coreLogic.HasGuides());
            SizeToolbarHost();
            ConfigureTransitions(positionSetting);

            if (!FlyoutWindowHelper.TryGetDisplayAreaAtCursor(out var displayArea) || displayArea is null)
            {
                displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
            }

            PositionOnDisplay(displayArea, positionSetting);
            _coreLogic.SetGuideEditMode(true);
            _toolbarVisible = true;
            _hasShownToolbar = true;
            Show();
            _ = DispatcherQueue.TryEnqueue(
                DispatcherQueuePriority.Low,
                () =>
                {
                    if (_disposed || !_toolbarVisible)
                    {
                        return;
                    }

                    Activate();
                    ApplyPseudoWindowHostChrome();
                    QueueLayoutDependentRefresh();
                });
        }

        private void OnShowing(TransparentWindow sender, ShowingEventArgs args)
        {
            ApplyPseudoWindowHostChrome();
            QueueLayoutDependentRefresh();
        }

        private void OnSurfaceLoaded(object sender, RoutedEventArgs args)
        {
            SubscribeToXamlRoot(Surface.XamlRoot);
            QueueLayoutDependentRefresh();
        }

        private void OnSurfaceUnloaded(object sender, RoutedEventArgs args)
        {
            SubscribeToXamlRoot(null);
        }

        private void OnSurfaceSizeChanged(object sender, SizeChangedEventArgs args)
        {
            QueueLayoutDependentRefresh();
        }

        private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
        {
            QueueLayoutDependentRefresh();
        }

        private void SubscribeToXamlRoot(XamlRoot xamlRoot)
        {
            if (ReferenceEquals(_subscribedXamlRoot, xamlRoot))
            {
                return;
            }

            if (_subscribedXamlRoot is not null)
            {
                _subscribedXamlRoot.Changed -= OnXamlRootChanged;
            }

            _subscribedXamlRoot = xamlRoot;
            if (_subscribedXamlRoot is not null)
            {
                _subscribedXamlRoot.Changed += OnXamlRootChanged;
            }
        }

        private void QueueLayoutDependentRefresh()
        {
            if (_layoutRefreshQueued || _disposed)
            {
                return;
            }

            _layoutRefreshQueued = true;
            if (!DispatcherQueue.TryEnqueue(
                DispatcherQueuePriority.Low,
                () =>
                {
                    _layoutRefreshQueued = false;
                    if (_disposed)
                    {
                        return;
                    }

                    UpdateOverlayExclusionBounds();
                    UpdateNonClientRegions();
                }))
            {
                _layoutRefreshQueued = false;
            }
        }

        private void ApplyPseudoWindowHostChrome()
        {
            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(false, false);

                // DefWindowProc only honors the non-client caption region reliably while a
                // sizing frame is present. Our WndProc removes that frame's painting and never
                // returns resize hit-test codes, so the user still cannot resize the toolbar.
                presenter.IsResizable = true;
            }

            // SetBorderAndTitleBar can update the HWND styles and DWM attributes, so apply the
            // transparent-window hardening after it, matching CmdPal's ordering.
            ApplyTransparentChrome();
            ApplyTransparentHostBackdrop();
        }

        private void ApplyTransparentHostBackdrop()
        {
            SystemBackdrop = new TransparentTintBackdrop
            {
                TintColor = global::Windows.UI.Color.FromArgb(0, 0, 0, 0),
            };
        }

        private void UpdateOverlayExclusionBounds()
        {
            if (_disposed)
            {
                return;
            }

            SetOverlayExclusionBounds(
                new RectInt32(AppWindow.Position.X, AppWindow.Position.Y, AppWindow.Size.Width, AppWindow.Size.Height));
        }

        private void SetOverlayExclusionBounds(RectInt32 bounds)
        {
            if (_coreLogic is null)
            {
                return;
            }

            _coreLogic.SetToolbarBoundingBox(
                bounds.X,
                bounds.Y,
                bounds.X + bounds.Width,
                bounds.Y + bounds.Height);
        }

        /// <summary>
        /// Registers the drag grip's physical rectangle as the only
        /// <see cref="NonClientRegionKind.Caption"/> region, so the native move loop - not manual
        /// pointer tracking - handles pointer drags.
        /// </summary>
        private void UpdateNonClientRegions()
        {
            if (Surface.XamlRoot is null || Surface.ActualWidth <= 0 || Surface.ActualHeight <= 0 || AppWindow is null)
            {
                return;
            }

            double scale = Surface.XamlRoot.RasterizationScale;

            RectInt32 ToPhysicalRect(FrameworkElement element)
            {
                var originDip = element.TransformToVisual(null).TransformPoint(new Point(0, 0));
                return new RectInt32(
                    (int)Math.Round(originDip.X * scale),
                    (int)Math.Round(originDip.Y * scale),
                    (int)Math.Ceiling(element.ActualWidth * scale),
                    (int)Math.Ceiling(element.ActualHeight * scale));
            }

            var nonClientInputSource = InputNonClientPointerSource.GetForWindowId(AppWindow.Id);

            var gripRect = ToPhysicalRect(DragGrip);
            nonClientInputSource.SetRegionRects(NonClientRegionKind.Caption, new[] { gripRect });
            nonClientInputSource.SetRegionRects(NonClientRegionKind.Passthrough, Array.Empty<RectInt32>());
        }

        private void StackPanel_Loaded(object sender, RoutedEventArgs e)
        {
            SelectDefaultMeasureStyle();
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            Dispose();
        }

        private void DragGrip_Click(object sender, RoutedEventArgs e)
        {
            Activate();
            if (!PostMessage(_hwnd, WmSysCommand, ScMove | HtCaption, 0))
            {
                Logger.LogWarning($"Failed to start the Screen Ruler system move command. Error: {Marshal.GetLastWin32Error()}");
            }
        }

        private nint WindowProc(nint hwnd, uint message, nuint wParam, nint lParam)
        {
            if (message == WmNcHitTest && IsInTransparentHostPadding(lParam))
            {
                return HtTransparent;
            }

            if (message == WmNcLeftButtonDoubleClick)
            {
                return 0;
            }

            // Match CmdPal's borderless pseudo-window handling: retain the sizing-frame style
            // needed for native caption input, but claim the entire HWND as client area so DWM
            // does not paint a second frame or shadow around the transparent host.
            if (message == WmNcCalcSize && wParam != 0)
            {
                return 0;
            }

            if (message == WmNcActivate && _originalWndProc != 0)
            {
                return CallWindowProc(_originalWndProc, hwnd, message, wParam, -1);
            }

            return _originalWndProc != 0
                ? CallWindowProc(_originalWndProc, hwnd, message, wParam, lParam)
                : DefWindowProc(hwnd, message, wParam, lParam);
        }

        private bool IsInTransparentHostPadding(nint lParam)
        {
            if (_disposed || Surface.XamlRoot is null || Surface.ActualWidth <= 0 || Surface.ActualHeight <= 0)
            {
                return false;
            }

            long packedPoint = lParam;
            int screenX = unchecked((short)(packedPoint & 0xFFFF));
            int screenY = unchecked((short)((packedPoint >> 16) & 0xFFFF));

            NativePoint clientOrigin = default;
            if (!ClientToScreen(_hwnd, ref clientOrigin))
            {
                return false;
            }

            double scale = Surface.XamlRoot.RasterizationScale;
            var surfaceOriginDip = Surface.TransformToVisual(null).TransformPoint(default);
            var shadowPadding = Surface.Padding;
            int cardLeft = clientOrigin.X + (int)Math.Round((surfaceOriginDip.X + shadowPadding.Left) * scale);
            int cardTop = clientOrigin.Y + (int)Math.Round((surfaceOriginDip.Y + shadowPadding.Top) * scale);
            int cardWidth = (int)Math.Ceiling((Surface.ActualWidth - shadowPadding.Left - shadowPadding.Right) * scale);
            int cardHeight = (int)Math.Ceiling((Surface.ActualHeight - shadowPadding.Top - shadowPadding.Bottom) * scale);

            return screenX < cardLeft ||
                   screenX >= cardLeft + cardWidth ||
                   screenY < cardTop ||
                   screenY >= cardTop + cardHeight;
        }

        private void UpdateToolUsageCompletionEvent(Action clearSelection)
        {
            _coreLogic.SetToolCompletionEvent(new PowerToys.MeasureToolCore.ToolSessionCompleted(() =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    clearSelection();
                });
            }));
        }

        private void HandleToolClick(ToggleButton button, Action startToolAction)
        {
            if (button.IsChecked.GetValueOrDefault())
            {
                btnSpacing.IsChecked = false;
                _coreLogic.ResetState();
                UpdateToolUsageCompletionEvent(() => button.IsChecked = false);
                startToolAction();
            }
            else
            {
                _coreLogic.ResetState();
            }
        }

        private void BoundsTool_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton button)
            {
                throw new ArgumentException("Bounds tool sender must be a ToggleButton.", nameof(sender));
            }

            HandleToolClick(button, () => _coreLogic.StartBoundsTool());
        }

        private void AddHorizontalGuide_Click(object sender, RoutedEventArgs e)
        {
            _coreLogic.BeginGuidePlacement(PowerToys.MeasureToolCore.GuideOrientation.Horizontal);
        }

        private void AddVerticalGuide_Click(object sender, RoutedEventArgs e)
        {
            _coreLogic.BeginGuidePlacement(PowerToys.MeasureToolCore.GuideOrientation.Vertical);
        }

        private void ClearGuides_Click(object sender, RoutedEventArgs e)
        {
            _coreLogic.ClearGuides();
        }

        private void SpacingTool_IsCheckedChanged(ToggleSplitButton sender, ToggleSplitButtonIsCheckedChangedEventArgs args)
        {
            if (sender.IsChecked)
            {
                StartSelectedSpacingTool();
            }
            else
            {
                _coreLogic.ResetState();
            }
        }

        private void StartSelectedSpacingTool()
        {
            btnBounds.IsChecked = false;
            _coreLogic.ResetState();
            UpdateToolUsageCompletionEvent(() => btnSpacing.IsChecked = false);

            (bool horizontal, bool vertical) = _selectedSpacingStyle switch
            {
                MeasureToolMeasureStyle.Spacing => (true, true),
                MeasureToolMeasureStyle.HorizontalSpacing => (true, false),
                MeasureToolMeasureStyle.VerticalSpacing => (false, true),
                _ => throw new InvalidOperationException($"Unsupported spacing style: {_selectedSpacingStyle}"),
            };

            _coreLogic.StartMeasureTool(horizontal, vertical);
        }

        private void SpacingMode_Click(object sender, RoutedEventArgs e)
        {
            MeasureToolMeasureStyle style = sender switch
            {
                RadioMenuFlyoutItem item when ReferenceEquals(item, SpacingModeBothItem) => MeasureToolMeasureStyle.Spacing,
                RadioMenuFlyoutItem item when ReferenceEquals(item, SpacingModeHorizontalItem) => MeasureToolMeasureStyle.HorizontalSpacing,
                RadioMenuFlyoutItem item when ReferenceEquals(item, SpacingModeVerticalItem) => MeasureToolMeasureStyle.VerticalSpacing,
                _ => throw new ArgumentException("Unknown spacing mode menu item.", nameof(sender)),
            };

            bool restartActiveTool = btnSpacing.IsChecked && _selectedSpacingStyle != style;
            bool shouldActivate = restartActiveTool || !btnSpacing.IsChecked;

            SelectSpacingMode(style, (RadioMenuFlyoutItem)sender, activate: false);
            _restartSpacingAfterFlyoutClose = shouldActivate ? restartActiveTool : null;
            SpacingModeFlyout.Hide();
        }

        private void SpacingModeFlyout_Closed(object sender, object e)
        {
            bool? restartActiveTool = _restartSpacingAfterFlyoutClose;
            _restartSpacingAfterFlyoutClose = null;

            if (restartActiveTool is bool restart)
            {
                ActivateSelectedSpacingMode(restart);
            }
        }

        private void SelectSpacingMode(MeasureToolMeasureStyle style, RadioMenuFlyoutItem item, bool activate)
        {
            bool restartActiveTool = btnSpacing.IsChecked && _selectedSpacingStyle != style;

            _selectedSpacingStyle = style;
            item.IsChecked = true;

            SpacingModeBothIcon.Visibility = style == MeasureToolMeasureStyle.Spacing ? Visibility.Visible : Visibility.Collapsed;
            SpacingModeHorizontalIcon.Visibility = style == MeasureToolMeasureStyle.HorizontalSpacing ? Visibility.Visible : Visibility.Collapsed;
            SpacingModeVerticalIcon.Visibility = style == MeasureToolMeasureStyle.VerticalSpacing ? Visibility.Visible : Visibility.Collapsed;

            string accessibleName = AutomationProperties.GetName(item);
            AutomationProperties.SetName(btnSpacing, accessibleName);
            SpacingModeToolTipText.Text = AutomationProperties.GetHelpText(item);

            if (!activate)
            {
                return;
            }

            ActivateSelectedSpacingMode(restartActiveTool);
        }

        private void ActivateSelectedSpacingMode(bool restartActiveTool)
        {
            if (restartActiveTool)
            {
                StartSelectedSpacingTool();
            }
            else if (!btnSpacing.IsChecked)
            {
                btnSpacing.IsChecked = true;
            }
        }

        private RadioMenuFlyoutItem GetSpacingModeItem(MeasureToolMeasureStyle style)
        {
            return style switch
            {
                MeasureToolMeasureStyle.Spacing => SpacingModeBothItem,
                MeasureToolMeasureStyle.HorizontalSpacing => SpacingModeHorizontalItem,
                MeasureToolMeasureStyle.VerticalSpacing => SpacingModeVerticalItem,
                _ => throw new ArgumentOutOfRangeException(nameof(style), style, "Not a spacing style."),
            };
        }

        private void SpacingKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            MeasureToolMeasureStyle style = sender.Key switch
            {
                Windows.System.VirtualKey.Number2 => MeasureToolMeasureStyle.Spacing,
                Windows.System.VirtualKey.Number3 => MeasureToolMeasureStyle.HorizontalSpacing,
                Windows.System.VirtualKey.Number4 => MeasureToolMeasureStyle.VerticalSpacing,
                _ => throw new ArgumentOutOfRangeException(nameof(sender), sender.Key, "Unknown spacing keyboard accelerator."),
            };

            if (_selectedSpacingStyle == style)
            {
                btnSpacing.IsChecked = !btnSpacing.IsChecked;
            }
            else
            {
                SelectSpacingMode(style, GetSpacingModeItem(style), activate: true);
            }

            args.Handled = true;
        }

        private void ClosePanelTool_Click(object sender, RoutedEventArgs e)
        {
            DismissToolbar();
        }

        public void ToggleToolbarVisibility()
        {
            if (_disposed || _shuttingDown)
            {
                return;
            }

            if (_toolbarVisible)
            {
                DismissToolbar();
            }
            else
            {
                ShowToolbar();
            }
        }

        public void Shutdown()
        {
            if (_disposed || _shuttingDown)
            {
                return;
            }

            _shuttingDown = true;
            _toolbarVisible = false;
            ResetMeasurementTools();
            _coreLogic?.SetGuideEditMode(false);
            SetOverlayExclusionBounds(default);
            Close();
        }

        private void DismissToolbar()
        {
            if (_disposed || _shuttingDown)
            {
                return;
            }

            ResetMeasurementTools();
            _coreLogic.SetGuideEditMode(false);

            if (!_coreLogic.HasGuides())
            {
                Shutdown();
                return;
            }

            _toolbarVisible = false;
            SetOverlayExclusionBounds(default);
            Hide();
        }

        private void ResetMeasurementTools()
        {
            _restartSpacingAfterFlyoutClose = null;
            SpacingModeFlyout.Hide();
            btnBounds.IsChecked = false;
            btnSpacing.IsChecked = false;
            _coreLogic?.ResetState();
        }

        private void SelectDefaultMeasureStyle()
        {
            MeasureToolMeasureStyle defaultStyle = settings.DefaultMeasureStyle;
            if (defaultStyle == MeasureToolMeasureStyle.None)
            {
                return;
            }

            if (defaultStyle == MeasureToolMeasureStyle.Bounds)
            {
                var peer = FrameworkElementAutomationPeer.FromElement(btnBounds) as ToggleButtonAutomationPeer;
                peer.Toggle();
                return;
            }

            if (defaultStyle is MeasureToolMeasureStyle.Spacing or
                MeasureToolMeasureStyle.HorizontalSpacing or
                MeasureToolMeasureStyle.VerticalSpacing)
            {
                SelectSpacingMode(defaultStyle, GetSpacingModeItem(defaultStyle), activate: true);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _shuttingDown = true;
            _toolbarVisible = false;
            Closed -= MainWindow_Closed;
            AppWindow.Changed -= OnAppWindowChanged;
            Showing -= OnShowing;
            Surface.Loaded -= OnSurfaceLoaded;
            Surface.Unloaded -= OnSurfaceUnloaded;
            Surface.SizeChanged -= OnSurfaceSizeChanged;
            SubscribeToXamlRoot(null);

            if (_coreLogic is not null)
            {
                _coreLogic.SetGuidePresenceChangedEvent(null);
                _coreLogic.SetGuideEditMode(false);
            }

            if (_originalWndProc != 0)
            {
                _ = SetWindowLongPtr(_hwnd, GwlWndProc, _originalWndProc);
                _originalWndProc = 0;
            }

            _coreLogic?.Dispose();
            _coreLogic = null;
            GC.SuppressFinalize(this);
        }

        private void KeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (args.Element is ToggleButton toggle)
            {
                var peer = new ToggleButtonAutomationPeer(toggle);
                peer.Toggle();
                args.Handled = true;
            }
            else if (args.Element is Button button)
            {
                var peer = new ButtonAutomationPeer(button);
                if (peer.GetPattern(PatternInterface.Invoke) is IInvokeProvider provider)
                {
                    provider.Invoke();
                    args.Handled = true;
                }
            }
        }

        [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(nint hWnd, uint message, nuint wParam, nint lParam);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint newLong);

        [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
        private static extern nint CallWindowProc(nint previousWindowProc, nint hWnd, uint message, nuint wParam, nint lParam);

        [DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
        private static extern nint DefWindowProc(nint hWnd, uint message, nuint wParam, nint lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ClientToScreen(nint hWnd, ref NativePoint point);

        private delegate nint WindowProcDelegate(nint hwnd, uint message, nuint wParam, nint lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }
    }
}
