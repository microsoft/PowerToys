// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using Microsoft.UI;                 // Added: For WindowId
using Microsoft.UI.Windowing;       // Added: For AppWindow & OverlappedPresenter
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;      // Added: For PointerRoutedEventArgs
using Windows.Graphics;             // Added: For PointInt32
using WinRT.Interop;                // Added: For WindowNative (to get HWND)

namespace KeystrokeOverlayUI
{
    public sealed partial class MainWindow : Window
    {
        // Uncommented this so the assignment in the constructor compiles
        private readonly IUserSettings _userSettings;

        private AppWindow _appWindow;
        private KeystrokeListener _listener;

        public OverlaySettings Settings { get; }

        // This collection holds the keys currently being displayed
        private ObservableCollection<KeyModel> ActiveKeys { get; } = new ObservableCollection<KeyModel>();

        // DRAGGING LOGIC VARS
        private bool _isDragging;
        private PointInt32 _lastMousePos;

        public MainWindow()
        {
            InitializeComponent();

            // Note: ExtendsContentIntoTitleBar works best when you have a standard title bar.
            // If ConfigureAppWindow removes the borders, this might not be visually necessary,
            // but it is harmless to keep.
            this.ExtendsContentIntoTitleBar = true;

            // 1. Get the AppWindow (Standard helper method logic)
            _appWindow = GetAppWindowForCurrentWindow();

            // 2. Configure it (Overlay style)
            ConfigureAppWindow();

            _userSettings = App.GetService<IUserSettings>();
            Settings = new OverlaySettings();

            _listener = new KeystrokeListener();
            _listener.OnBatchReceived += Listener_OnBatchReceived;
            _listener.Start();
        }

        // =========================================================
        //  HELPER METHODS (ADDED)
        // =========================================================
        private AppWindow GetAppWindowForCurrentWindow()
        {
            // Get the window handle (HWND) of the current WinUI 3 Window
            IntPtr hWnd = WindowNative.GetWindowHandle(this);

            // Retrieve the WindowId needed to get the AppWindow
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);

            // Return the AppWindow instance
            return AppWindow.GetFromWindowId(wndId);
        }

        private void ConfigureAppWindow()
        {
            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                // OVERLAY CONFIGURATION:
                // 1. Remove the system title bar and borders
                presenter.SetBorderAndTitleBar(false, false);

                // 2. Prevent resizing/maximizing by the user (since it's an overlay)
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;

                // 3. Keep it on top of other windows
                presenter.IsAlwaysOnTop = true;
            }
        }

        // =========================================================
        //  EVENT HANDLERS
        // =========================================================
        private void Listener_OnBatchReceived(KeystrokeBatch batch)
        {
            // Marshall to UI Thread
            this.DispatcherQueue.TryEnqueue(() =>
            {
                foreach (var keyEvent in batch.events)
                {
                    // Only showing "Down" events for visual simplicity
                    if (keyEvent.t == "down")
                    {
                        // Logic to format text (e.g., translate VK Code to String if 'text' is empty)
                        string displayText = string.IsNullOrEmpty(keyEvent.text)
                                            ? ((Windows.System.VirtualKey)keyEvent.vk).ToString()
                                            : keyEvent.text;

                        ShowKey(displayText);
                    }
                }
            });
        }

        private void ShowKey(string keyText)
        {
            var model = new KeyModel
            {
                Text = keyText,
                Settings = this.Settings,
            };

            ActiveKeys.Add(model);

            // Use a DispatcherTimer to remove the key after the OverlayTimeout
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(Settings.OverlayTimeout),
            };
            timer.Tick += (s, e) =>
            {
                ActiveKeys.Remove(model);
                timer.Stop();
            };
            timer.Start();
        }

        // =========================================================
        //  DRAGGING LOGIC
        // =========================================================
        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (Settings.IsDraggable)
            {
                // Get mouse position in screen coordinates
                _lastMousePos = _appWindow.Position;
                var pointerPos = e.GetCurrentPoint(null).Position;
                _lastMousePos.X = (int)pointerPos.X;
                _lastMousePos.Y = (int)pointerPos.Y;

                _isDragging = true;
                (sender as UIElement)?.CapturePointer(e.Pointer);
            }
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                // Get current mouse position
                var pointerPos = e.GetCurrentPoint(null).Position;
                int newX = (int)pointerPos.X;
                int newY = (int)pointerPos.Y;

                // Calculate offset
                int deltaX = newX - _lastMousePos.X;
                int deltaY = newY - _lastMousePos.Y;

                // Get current window position
                var windowPos = _appWindow.Position;

                // Move the window using AppWindow
                _appWindow.Move(new PointInt32(windowPos.X + deltaX, windowPos.Y + deltaY));

                // Update last pos
                _lastMousePos.X = newX;
                _lastMousePos.Y = newY;
            }
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _isDragging = false;
            (sender as UIElement)?.ReleasePointerCapture(e.Pointer);
        }
    }

    // single key display
    public class KeyModel
    {
        public string Text { get; set; }

        public OverlaySettings Settings { get; set; }
    }
}
