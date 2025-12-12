// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;

using KeystrokeOverlayUI.Controls;
using KeystrokeOverlayUI.Helpers;
using KeystrokeOverlayUI.Models;
using KeystrokeOverlayUI.Services;

using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinRT.Interop;

namespace KeystrokeOverlayUI
{
    // Main overlay window.
    public sealed partial class MainWindow : Window, IDisposable
    {
        public MainViewModel ViewModel { get; set; } = new();

        // readonly constants
        private readonly InputCursor _dragCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
        private readonly DispatcherTimer _zOrderEnforcer = new();

        // core components
        private readonly KeystrokeListener _keystrokeListener = new();
        private readonly OverlaySettings _overlaySettings = new();
        private CancellationTokenSource _startupCancellationSource;
        private bool _disposed;

        // draggable overlay
        private bool _isDragging;
        private NativeWindowHelper.POINT _lastCursorPos;

        public MainWindow()
        {
            InitializeComponent();
            SystemBackdrop = new MicaBackdrop() { Kind = MicaKind.BaseAlt };
            ForceWindowOnTop();

            Activated += (s, e) => ApplyOverlayStyles();
            _zOrderEnforcer.Interval = TimeSpan.FromMilliseconds(500);
            _zOrderEnforcer.Tick += (s, e) => ForceWindowOnTop();

            RootGrid.DataContext = ViewModel;

            _overlaySettings.SettingsUpdated += (props) =>
            {
                DispatcherQueue.TryEnqueue(() => ViewModel.ApplySettings(props));
            };

            _overlaySettings.Initialize();

            _keystrokeListener.OnBatchReceived += OnKeyReceived;
            _keystrokeListener.Start();

            ConfigureOverlayWindow();
            RunStartupSequence(isDraggable: ViewModel.IsDraggable);

            ViewModel.HotkeyActionTriggered += OnHotkeyActionTriggered;

            ViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.IsDraggable))
                {
                    RunStartupSequence(isDraggable: ViewModel.IsDraggable);
                }
            };
        }

        private void ApplyOverlayStyles()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            NativeWindowHelper.SetOverlayWindowStyles(hWnd);
            NativeWindowHelper.SetRoundedCorners(hWnd);

            ForceWindowOnTop();
        }

        private void ConfigureOverlayWindow()
        {
            var appWindow = GetAppWindow();

            if (appWindow != null)
            {
                var presenter = appWindow.Presenter as OverlappedPresenter
                                ?? OverlappedPresenter.Create();

                appWindow.SetPresenter(presenter);

                presenter.IsAlwaysOnTop = true;
                presenter.IsResizable = false;
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = false;
                presenter.SetBorderAndTitleBar(false, false);
            }
        }

        private async void RunStartupSequence(bool isDraggable)
        {
            _startupCancellationSource?.Cancel();
            _startupCancellationSource?.Dispose();
            _startupCancellationSource = new CancellationTokenSource();

            var token = _startupCancellationSource.Token;

            ForceWindowOnTop();
            if (!_zOrderEnforcer.IsEnabled)
            {
                _zOrderEnforcer.Start();
            }

            try
            {
                if (isDraggable)
                {
                    // Loop with cancellation check
                    for (int index = 10; index > 0; index--)
                    {
                        ViewModel.RegisterKey($"Drag to Position ({index})", durationMs: 1000, textSize: 30);
                        await Task.Delay(1000, token);
                    }
                }

                // cleanup
                ViewModel.ClearKeys();
                await Task.Delay(500, token);
            }
            catch (OperationCanceledException)
            {
                ViewModel.ClearKeys();
            }
            finally
            {
                // cleanup
                if (_startupCancellationSource != null)
                {
                    _startupCancellationSource.Dispose();
                    _startupCancellationSource = null;
                }
            }
        }

        private void OnKeyReceived(KeystrokeEvent kEvent)
        {
            if (_startupCancellationSource != null && !_startupCancellationSource.IsCancellationRequested)
            {
                _startupCancellationSource.Cancel();
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                ViewModel.HandleKeystrokeEvent(kEvent);

                if (!_zOrderEnforcer.IsEnabled)
                {
                    _zOrderEnforcer.Start();
                }
            });
        }

        // ----------------------
        // Hotkey Methods
        // ----------------------
        private void OnHotkeyActionTriggered(object sender, HotkeyAction action)
        {
            switch (action)
            {
                case HotkeyAction.Monitor:
                    MoveToNextMonitor();
                    break;
                case HotkeyAction.DisplayMode:
                case HotkeyAction.Activation:
                    HandleActivation();
                    break;
            }

            ForceWindowOnTop();

            // resize to show labels
            RootGrid.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var desiredSize = RootGrid.DesiredSize;
            ResizeAppWindow(desiredSize.Width + 5, desiredSize.Height + 15);
        }

        private void HandleActivation()
        {
            ShowAppWindow();
        }

        private void MoveToNextMonitor()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(wndId);

            if (appWindow == null)
            {
                return;
            }

            var displayAreas = DisplayArea.FindAll();
            if (displayAreas.Count <= 1)
            {
                return;
            }

            // Find current display index
            DisplayArea currentDisplay = DisplayArea.GetFromWindowId(wndId, DisplayAreaFallback.Primary);

            int currentIndex = -1;
            for (int i = 0; i < displayAreas.Count; i++)
            {
                if (displayAreas[i].DisplayId.Value == currentDisplay.DisplayId.Value)
                {
                    currentIndex = i;
                    break;
                }
            }

            // Calculate Next Index
            int nextIndex = (currentIndex + 1) % displayAreas.Count;
            DisplayArea nextDisplay = displayAreas[nextIndex];

            // move to Top-Left of new monitor
            int newX = nextDisplay.WorkArea.X + 15;
            int newY = nextDisplay.WorkArea.Y + 12;

            appWindow.Move(new Windows.Graphics.PointInt32(newX, newY));
            ViewModel.ShowLabel(HotkeyAction.Monitor, $"Monitor {nextIndex + 1}");
        }

        // ----------------------
        // Draggable Overlay
        // ----------------------
        private void SetRootGridCursor(InputCursor cursor)
        {
            // Use Reflection to access the protected "ProtectedCursor" property on the Border (RootGrid)
            typeof(UIElement)
                .GetProperty("ProtectedCursor", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(RootGrid, cursor);
        }

        private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var properties = e.GetCurrentPoint(RootGrid).Properties;

            if (ViewModel.IsDraggable && properties.IsLeftButtonPressed)
            {
                _isDragging = true;
                RootGrid.CapturePointer(e.Pointer);
                _lastCursorPos = NativeWindowHelper.GetCursorPosition();

                SetRootGridCursor(_dragCursor);
                e.Handled = true;
            }
        }

        private void RootGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                var currentPos = NativeWindowHelper.GetCursorPosition();

                int deltaX = currentPos.X - _lastCursorPos.X;
                int deltaY = currentPos.Y - _lastCursorPos.Y;

                var appWindow = GetAppWindow();
                if (appWindow != null)
                {
                    var newPos = new Windows.Graphics.PointInt32(
                        appWindow.Position.X + deltaX,
                        appWindow.Position.Y + deltaY );

                    appWindow.Move(newPos);
                }

                _lastCursorPos = currentPos;
            }
        }

        private void RootGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                RootGrid.ReleasePointerCapture(e.Pointer);
                UpdateCursorState();
            }
        }

        private void RootGrid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            UpdateCursorState();
        }

        private void RootGrid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            SetRootGridCursor(null);
        }

        private void UpdateCursorState()
        {
            if (ViewModel.IsDraggable)
            {
                SetRootGridCursor(_dragCursor);
            }
            else
            {
                SetRootGridCursor(null);
            }
        }

        // ----------------------
        // WinUI Event Handlers
        // ----------------------
        private void RootGrid_Loaded(object sender, RoutedEventArgs e)
        {
            RootGrid.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var desiredSize = RootGrid.DesiredSize;
            ResizeAppWindow(desiredSize.Width + 5, desiredSize.Height + 15);
        }

        private void StackPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is not StackPanel stackPanel)
            {
                return;
            }

            stackPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var desiredSize = stackPanel.DesiredSize;

            if (desiredSize.Width == 0 ||
                desiredSize.Height == 0 ||
                ViewModel.PressedKeys.Count == 0)
            {
                if (!ViewModel.IsVisibleHotkey)
                {
                    HideAppWindow();
                    _zOrderEnforcer.Stop();
                }

                return;
            }

            ShowAppWindow();

            double totalWidth =
                desiredSize.Width + RootGrid.Padding.Left + RootGrid.Padding.Right + 5;

            double totalHeight =
                desiredSize.Height + RootGrid.Padding.Top + RootGrid.Padding.Bottom + 15;

            if (ViewModel.IsVisibleHotkey)
            {
                totalHeight = totalHeight + 30;
            }

            ResizeAppWindow(totalWidth, totalHeight);
            ForceWindowOnTop();
        }

        // ----------------------
        // Window Helper Methods
        // ----------------------
        private AppWindow GetAppWindow()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            return AppWindow.GetFromWindowId(wndId);
        }

        private void HideAppWindow()
        {
            var appWindow = GetAppWindow();
            appWindow?.Hide();
        }

        private void ShowAppWindow()
        {
            var appWindow = GetAppWindow();
            appWindow?.Show();
        }

        private void ResizeAppWindow(double widthDIPs, double heightDIPs)
        {
            var appWindow = GetAppWindow();

            if (appWindow != null)
            {
                double scale = RootGrid.XamlRoot.RasterizationScale;

                int windowWidth = (int)Math.Ceiling(widthDIPs * scale);
                int windowHeight = (int)Math.Ceiling(heightDIPs * scale);

                if (appWindow.Size.Width != windowWidth ||
                    appWindow.Size.Height != windowHeight)
                {
                    appWindow.Resize(new Windows.Graphics.SizeInt32(windowWidth, windowHeight));
                }
            }
        }

        private void ForceWindowOnTop()
        {
            if (ViewModel.PressedKeys.Count == 0)
            {
                _zOrderEnforcer.Stop();
                return;
            }

            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            NativeWindowHelper.EnforceTopMost(hWnd);
        }

        // -------------------
        // Other Methods
        // -------------------
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _keystrokeListener?.Dispose();
                    _overlaySettings?.Dispose();
                }

                _disposed = true;
            }
        }

        ~MainWindow()
        {
            Dispose(false);
        }
    }
}
