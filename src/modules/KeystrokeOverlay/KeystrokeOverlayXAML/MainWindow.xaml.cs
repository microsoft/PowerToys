// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;

using KeystrokeOverlayUI.Controls;
using KeystrokeOverlayUI.Models;
using Microsoft.UI;
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
    /// <summary>
    /// Main overlay window.
    /// </summary>
    public sealed partial class MainWindow : Window, IDisposable
    {
        public MainViewModel ViewModel { get; set; } = new();

        private readonly KeystrokeListener _keystrokeListener = new();
        private readonly OverlaySettings _overlaySettings = new();
        private CancellationTokenSource _startupCancellationSource;
        private bool _disposed;

        // for draggable overlay
        private const int WMNCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        // always on top
        // P/Invoke for Win32 APIs
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private static readonly IntPtr HWNDTOPMOST = new IntPtr(-1);
        private const uint SWPNOSIZE = 0x0001;
        private const uint SWPNOMOVE = 0x0002;
        private const uint SWPNOACTIVATE = 0x0010;
        private const uint SWPSHOWWINDOW = 0x0040;

        private const int GWLEXSTYLE = -20;
        private const int EXNOACTIVATE = 0x08000000;
        private const int EXTOPMOST = 0x00000008;
        private const int EXTOOLWINDOW = 0x00000080;

        private readonly DispatcherTimer _zOrderEnforcer = new();

        public MainWindow()
        {
            SystemBackdrop = null;

            Content = new Grid
            {
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
            };

            InitializeComponent();

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

            ViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.IsDraggable))
                {
                    RunStartupSequence(isDraggable: ViewModel.IsDraggable);
                }
            };

            Activated += (s, e) => ApplyOverlayStyles();
            _zOrderEnforcer.Interval = TimeSpan.FromMilliseconds(500);
            _zOrderEnforcer.Tick += (s, e) => ForceWindowOnTop();
        }

        private void ApplyOverlayStyles()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);

            int exStyle = GetWindowLong(hWnd, GWLEXSTYLE);

            // WS_EX_NOACTIVATE: Prevents the window from stealing focus when clicked/updated
            // WS_EX_TOOLWINDOW: Hides it from the Alt+Tab menu (optional, good for overlays)
            _ = SetWindowLong(hWnd, GWLEXSTYLE, exStyle | EXNOACTIVATE | EXTOOLWINDOW);

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

            try
            {
                if (isDraggable)
                {
                    // Loop with cancellation check
                    for (int index = 10; index > 0; index--)
                    {
                        // 2. Pass the token to Task.Delay
                        // If cancelled, this throws OperationCanceledException immediately
                        ViewModel.RegisterKey($"Drag to Position ({index})", durationMs: 1000, textSize: 30);
                        await Task.Delay(1000, token);
                    }
                }

                // Normal completion cleanup
                ViewModel.ClearKeys();
                await Task.Delay(500, token);
            }
            catch (OperationCanceledException)
            {
                // 3. Logic for when a key interrupts the sequence
                // We clear immediately so the new key can take over
                ViewModel.ClearKeys();
            }
            finally
            {
                // Clean up the source
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
        // WinUI Event Handlers
        // ----------------------
        private void RootGrid_Loaded(object sender, RoutedEventArgs e)
        {
            RootGrid.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var desiredSize = RootGrid.DesiredSize;
            ResizeAppWindow(desiredSize.Width, desiredSize.Height);
        }

        private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var properties = e.GetCurrentPoint(RootGrid).Properties;

            if (ViewModel.IsDraggable && properties.IsLeftButtonPressed)
            {
                IntPtr hWnd = WindowNative.GetWindowHandle(this);

                ReleaseCapture();

                _ = SendMessage(hWnd, WMNCLBUTTONDOWN, HTCAPTION, 0);

                e.Handled = true;
            }
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
                HideAppWindow();
                _zOrderEnforcer.Stop();
                return;
            }

            ShowAppWindow();

            double totalWidth =
                desiredSize.Width + RootGrid.Padding.Left + RootGrid.Padding.Right;

            double totalHeight =
                desiredSize.Height + RootGrid.Padding.Top + RootGrid.Padding.Bottom + 5;

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

            // SWP_NOACTIVATE is important here to ensure we don't steal focus while typing
            SetWindowPos(hWnd, HWNDTOPMOST, 0, 0, 0, 0, SWPNOMOVE | SWPNOSIZE | SWPSHOWWINDOW | SWPNOACTIVATE);
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
