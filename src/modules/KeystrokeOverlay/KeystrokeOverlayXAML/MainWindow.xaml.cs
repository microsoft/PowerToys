// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public MainWindow()
        {
            SystemBackdrop = null;

            Content = new Grid
            {
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
            };

            InitializeComponent();

            HideAppWindow();

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

            HideAppWindow();
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
            });
        }

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
                return;
            }

            ShowAppWindow();

            double totalWidth =
                desiredSize.Width + RootGrid.Padding.Left + RootGrid.Padding.Right;

            double totalHeight =
                desiredSize.Height + RootGrid.Padding.Top + RootGrid.Padding.Bottom + 5;

            ResizeAppWindow(totalWidth, totalHeight);
        }

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
