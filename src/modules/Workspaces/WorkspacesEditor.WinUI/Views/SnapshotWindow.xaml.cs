// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

using WinRT.Interop;
using WorkspacesEditor.Helpers;
using WorkspacesEditor.ViewModels;

namespace WorkspacesEditor.Views
{
    public sealed partial class SnapshotWindow : Window
    {
        private readonly MainViewModel _mainViewModel;
        private readonly Microsoft.UI.Xaml.DispatcherTimer _pulseTimer;
        private bool _captured;
        private bool _dotVisible = true;

        public SnapshotWindow(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            this.InitializeComponent();

            this.Title = ResourceLoaderInstance.ResourceLoader?.GetString("SnapshotWindowTitle") ?? "Snapshot Creator";
            DescriptionText.Text = ResourceLoaderInstance.ResourceLoader?.GetString("SnapshotDescription") ?? "Edit your layout and click \"Capture\" when finished.";
            SnapshotButton.Content = ResourceLoaderInstance.ResourceLoader?.GetString("Take_Snapshot") ?? "Capture";
            CancelButton.Content = ResourceLoaderInstance.ResourceLoader?.GetString("Cancel") ?? "Cancel";

            // Configure window: small, centered, no resize, topmost
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new Windows.Graphics.SizeInt32(420, 200));

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsAlwaysOnTop = true;
            }

            // Center on primary display
            var displayArea = DisplayArea.Primary;
            var workArea = displayArea.WorkArea;
            int x = workArea.X + ((workArea.Width - 420) / 2);
            int y = workArea.Y + ((workArea.Height - 200) / 2);
            appWindow.Move(new Windows.Graphics.PointInt32(x, y));

            // Pulse the recording dot
            _pulseTimer = new Microsoft.UI.Xaml.DispatcherTimer();
            _pulseTimer.Interval = TimeSpan.FromMilliseconds(600);
            _pulseTimer.Tick += (s, e) =>
            {
                _dotVisible = !_dotVisible;
                RecordingDot.Opacity = _dotVisible ? 1.0 : 0.15;
            };
            _pulseTimer.Start();

            this.Closed += OnClosed;

            // Set focus to the Capture button when window loads
            this.Activated += (s, e) =>
            {
                var snapshotHwnd = WindowNative.GetWindowHandle(this);
                SetForegroundWindow(snapshotHwnd);
                SnapshotButton.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            };

            // Handle Escape key to cancel
            this.Content.KeyDown += (s, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.Escape)
                {
                    this.Close();
                }
            };
        }

        private void SnapshotButtonClicked(object sender, RoutedEventArgs e)
        {
            _captured = true;
            this.Close();
            _mainViewModel.SnapWorkspace();
        }

        private void CancelButtonClicked(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OnClosed(object sender, WindowEventArgs args)
        {
            _pulseTimer.Stop();
            if (!_captured)
            {
                _mainViewModel.CancelSnapshot();
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
