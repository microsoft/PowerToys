// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
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
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window, IDisposable
    {
        public MainViewModel ViewModel { get; set; } = new();

        private const bool DEBUGMODE = true;

        private readonly KeystrokeListener _keystrokeListener = new();

        private readonly OverlaySettings _overlaySettings = new();

        private bool _disposed;

        // P/Invoke constants and methods to allow dragging a borderless window
        private const int WMNCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        public MainWindow()
        {
            InitializeComponent();

            RootGrid.DataContext = ViewModel;

            UseThemeBackground();

            _overlaySettings.SettingsUpdated += (props) =>
            {
                DispatcherQueue.TryEnqueue(() => ViewModel.ApplySettings(props));
            };
            _overlaySettings.Initialize();

            _keystrokeListener.OnBatchReceived += OnKeyReceived;
            _keystrokeListener.Start();

            ConfigureOverlayWindow();

            RunStartupSequence(isDraggable: ViewModel.IsDraggable);

            // listener for changes to IsDraggable to re-run startup sequence
            ViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.IsDraggable))
                {
                    RunStartupSequence(isDraggable: ViewModel.IsDraggable);
                }
            };
        }

        private void UseThemeBackground()
        {
            // Use the theme background brush so the app matches the current theme.
            try
            {
                var themeBrush = Application.Current?.Resources["ApplicationPageBackgroundThemeBrush"] as Microsoft.UI.Xaml.Media.Brush;
                if (themeBrush != null)
                {
                    RootGrid.Background = themeBrush;
                }
            }
            catch
            {
                // Ignore if the resource isn't present — app will keep whatever background was set in XAML.
            }
        }

        private void ConfigureOverlayWindow()
        {
            // get app window
            var appWindow = GetAppWindow();

            if (appWindow != null)
            {
                // Set the presenter to Overlapped to manipulate window chrome
                var presenter = appWindow.Presenter as OverlappedPresenter;
                if (presenter == null)
                {
                    presenter = OverlappedPresenter.Create();
                    appWindow.SetPresenter(presenter);
                }

                // KEY SETTINGS FOR OVERLAY:
                presenter.IsAlwaysOnTop = true;       // Keep above other apps
                presenter.IsResizable = false;        // Fixed size (optional)
                presenter.IsMinimizable = false;      // Don't allow minimize
                presenter.IsMaximizable = false;      // Don't allow maximize
                presenter.SetBorderAndTitleBar(false, false); // Remove standard Windows chrome
            }
        }

        private async void RunStartupSequence(bool isDraggable = false)
        {
            if (isDraggable)
            {
                // 10 second duration for the "Drag to Position" message + positioning time
                ViewModel.RegisterKey("Drag to Position", durationMs: 5000, textSize: 40);
                await Task.Delay(5000);
            }

            // clear instructions + pause briefly
            ViewModel.ClearKeys();
            await Task.Delay(500);

            // start simulating test keys
            // change this to false to disable test keys on startup
            if (DEBUGMODE)
            {
                SimulateTestKeys();
            }
        }

        private async void SimulateTestKeys()
        {
            // Simulate some test key presses for demonstration
            string[] testKeys = { "A", "B", "C", "D", "E", "F", "G" };
            foreach (var key in testKeys)
            {
                ViewModel.RegisterKey(key);
                await Task.Delay(300);
            }
        }

        private void OnKeyReceived(KeystrokeEvent kEvent)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                // We typically only want to show the key when it is pressed down (IsPressed = true)
                // If you want to show releases, remove this check.
                if (kEvent.IsPressed)
                {
                    string keyName = GetKeyName(kEvent.VirtualKey);
                    ViewModel.RegisterKey(keyName);
                }
            });
        }

        private string GetKeyName(uint virtualKey)
        {
            // Simple conversion using built-in WinUI/System enums
            var key = (Windows.System.VirtualKey)virtualKey;

            return key switch
            {
                Windows.System.VirtualKey.Space => "Space",
                Windows.System.VirtualKey.Enter => "Enter",
                Windows.System.VirtualKey.Back => "Backspace",

                // Add other special cases here
                _ => key.ToString(),
            };
        }

        // ---------------------------
        // XAML Event Handlers
        // ---------------------------
        private void RootGrid_Loaded(object sender, RoutedEventArgs e)
        {
            RootGrid.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var desiredSize = RootGrid.DesiredSize;
            ResizeAppWindow(desiredSize.Width, desiredSize.Height);
        }

        private void RootGrid_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // Check if the left mouse button is pressed
            var properties = e.GetCurrentPoint(RootGrid).Properties;
            if (ViewModel.IsDraggable && properties.IsLeftButtonPressed)
            {
                // 1. Get the handle (hWnd) of the current window
                IntPtr hWnd = WindowNative.GetWindowHandle(this);

                // 2. Release the mouse capture from the XAML element
                // This is critical: XAML usually holds the mouse, preventing the OS from taking over.
                ReleaseCapture();

                // 3. Send the "Title Bar Clicked" message to the OS
                // This tricks Windows into thinking the user clicked a standard title bar,
                // so the OS handles the dragging logic natively.
                int result = SendMessage(hWnd, WMNCLBUTTONDOWN, HTCAPTION, 0);
                if (result == 0)
                {
                    ManagedCommon.Logger.LogWarning("SendMessage failed to initiate window drag.");
                }

                // Mark event as handled so it doesn't bubble up
                e.Handled = true;
            }
        }

        private void StackPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is not StackPanel stackPanel)
            {
                return;
            }

            // measure the StackPanel to get its desired size
            stackPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var desiredSize = stackPanel.DesiredSize;

            // hide window if no content
            if (desiredSize.Width == 0 || desiredSize.Height == 0 || ViewModel.PressedKeys.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("No content to display, hiding overlay.");
                HideAppWindow();
                return;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Showing Overlay Content");
                ShowAppWindow();
            }

            // calculate total size including padding
            double totalWidth = desiredSize.Width + RootGrid.Padding.Left + RootGrid.Padding.Right;
            double totalHeight = desiredSize.Height + RootGrid.Padding.Top + RootGrid.Padding.Bottom + 5;

            // resize the app window
            ResizeAppWindow(totalWidth, totalHeight);
        }

        // ---------------------------
        // AppWindow Helper Methods
        // ---------------------------
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

                // Always round UP (Ceiling) to prevent cutting off pixels
                int windowWidth = (int)Math.Ceiling(widthDIPs * scale);
                int windowHeight = (int)Math.Ceiling(heightDIPs * scale);

                if (appWindow.Size.Width != windowWidth || appWindow.Size.Height != windowHeight)
                {
                    appWindow.Resize(new Windows.Graphics.SizeInt32(windowWidth, windowHeight));
                }
            }
        }

        // ---------------------------
        // General Dispose Pattern
        // ---------------------------
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
