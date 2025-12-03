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
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; set; } = new();

        // P/Invoke constants and methods to allow dragging a borderless window
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        public MainWindow()
        {
            InitializeComponent();

            RootGrid.DataContext = ViewModel;

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

            //TEMP: Simulate key presses for testing
            RunStartupSequence();
            //SimulateTestKeys();

            ConfigureOverlayWindow();
        }

        private async void RunStartupSequence()
        {
            // STEP 1: Show the instruction message
            // We give it a long duration (e.g., 10 seconds) so it doesn't fade 
            // while the user is trying to grab it.
            ViewModel.RegisterKey("Drag to Position", durationMs: 10000, textSize: 40);

            // STEP 2: Wait for the user to position the window
            // This gives them 4 seconds to drag it before the test starts.
            await Task.Delay(5000);

            // STEP 3: Clear the instruction message
            ViewModel.ClearKeys();

            // Short pause for visual cleanliness
            await Task.Delay(500);

            // STEP 4: Start the actual simulation
            SimulateTestKeys();
        }

        private async void SimulateTestKeys()
        {
            string[] testKeys = { "A", "B", "C", "D", "E", "F", "G" };
            foreach (var key in testKeys)
            {
                ViewModel.RegisterKey(key);
                await Task.Delay(300);
            }
        }

        private void ConfigureOverlayWindow()
        {
            // Get the AppWindow for this XAML Window
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(wndId);

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

        private void RootGrid_Loaded(object sender, RoutedEventArgs e)
        {
            AdjustWindowToContent();
        }

        private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            //AdjustWindowToContent();
            if (sender is Border border)
            {
                // Access the new size from the event arguments
                double newWidth = e.NewSize.Width;
                double newHeight = e.NewSize.Height;

                // You can also get the actual dimensions using ActualWidth and ActualHeight
                double actualWidth = border.ActualWidth;
                double actualHeight = border.ActualHeight;

                // Perform any actions needed when the size changes
                //System.Diagnostics.Debug.WriteLine($"Border size changed to: {newWidth}x{newHeight}");
            }
        }

        private void AdjustWindowToContent()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(wndId);

            if (appWindow != null)
            {
                // 1. THE FIX: Ask the RootGrid how big it WANTS to be, 
                //    assuming it has infinite space (ignoring current window size).
                RootGrid.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                // 2. Use DesiredSize (what it wants) instead of ActualWidth (what it is currently forced to be)
                var desiredSize = RootGrid.DesiredSize;

                // 3. Get the scale factor
                double scale = RootGrid.XamlRoot.RasterizationScale;

                // 4. Calculate dimensions (Round UP to avoid cutting off pixels)
                int newWidth = (int)Math.Ceiling(desiredSize.Width * scale);
                int newHeight = (int)Math.Ceiling(desiredSize.Height * scale);

                // 5. Resize
                // We add a check to stop resizing if we are already at the right size
                if (appWindow.Size.Width != newWidth || appWindow.Size.Height != newHeight)
                {
                    appWindow.Resize(new Windows.Graphics.SizeInt32(newWidth, newHeight));
                }
            }
        }

        private void RootGrid_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // Check if the left mouse button is pressed
            var properties = e.GetCurrentPoint(RootGrid).Properties;
            if (properties.IsLeftButtonPressed)
            {
                // 1. Get the handle (hWnd) of the current window
                IntPtr hWnd = WindowNative.GetWindowHandle(this);

                // 2. Release the mouse capture from the XAML element
                // This is critical: XAML usually holds the mouse, preventing the OS from taking over.
                ReleaseCapture();

                // 3. Send the "Title Bar Clicked" message to the OS
                // This tricks Windows into thinking the user clicked a standard title bar,
                // so the OS handles the dragging logic natively.
                SendMessage(hWnd, WM_NCLBUTTONDOWN, HT_CAPTION, 0);

                // Mark event as handled so it doesn't bubble up
                e.Handled = true;
            }
        }

        private void StackPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 1. Get the StackPanel that holds the keys
            if (sender is not StackPanel stackPanel) return;

            // 2. Ask the StackPanel how big it WANTS to be (ignoring current window constraints)
            stackPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var desiredSize = stackPanel.DesiredSize;

            // 3. Add the padding from the outer Border (RootGrid)
            //    (Your XAML has Padding="15,12", so we must add that back in)
            double totalWidth = desiredSize.Width + RootGrid.Padding.Left + RootGrid.Padding.Right;
            double totalHeight = desiredSize.Height + RootGrid.Padding.Top + RootGrid.Padding.Bottom;

            // 4. Resize the window
            ResizeAppWindow(totalWidth, totalHeight);
        }

        private void ResizeAppWindow(double widthDIPs, double heightDIPs)
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(wndId);

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

        private string GetKeyName(uint virtualKey)
        {
            // Simple conversion using built-in WinUI/System enums
            // You can expand this for better symbols (e.g. "Command", "Option") later
            var key = (Windows.System.VirtualKey)virtualKey;

            return key switch
            {
                Windows.System.VirtualKey.Space => "Space",
                Windows.System.VirtualKey.Enter => "Enter",
                Windows.System.VirtualKey.Back => "Backspace",
                // Add other special cases here
                _ => key.ToString()
            };
        }
    }
}
