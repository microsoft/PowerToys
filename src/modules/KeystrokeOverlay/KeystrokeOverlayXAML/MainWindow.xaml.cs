// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using ManagedCommon;
using Microsoft.UI.Xaml;

namespace KeystrokeOverlayUI
{
    // single key display
    public class KeyModel
    {
        public string Text { get; set; }
    }

    public class MockSettings : INotifyPropertyChanged
    {
        // Defaults from KeystrokeOverlayProperties.cs
        private double _textSize = 24.0;
        private int _overlayTimeout = 3000;
        private Color _textColor = Colors.White;
        private double _textOpacity = 1.0; // 100%
        private Color _bgColor = Colors.Black;
        private double _bgOpacity = 0.5; // 50%
        private bool _isDraggable = true;

        public double TextSize
        {
            get => _textSize;
            set => SetProperty(ref _textSize, value);
        }

        public int OverlayTimeout
        {
            get => _overlayTimeout;
            set => SetProperty(ref _overlayTimeout, value);
        }

        public bool IsDraggable
        {
            get => _isDraggable;
            set => SetProperty(ref _isDraggable, value);
        }

        // --- Derived Brush properties for XAML Binding ---
        public Brush TextBrush => new SolidColorBrush(_textColor) { Opacity = _textOpacity };
        public Brush BackgroundBrush => new SolidColorBrush(_bgColor) { Opacity = _bgOpacity };

        // Helper for INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected void SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value)) return;
            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            
            // Update derived brushes
            if (propertyName == nameof(TextColor) || propertyName == nameof(TextOpacity))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextBrush)));
            if (propertyName == nameof(BackgroundColor) || propertyName == nameof(BackgroundOpacity))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundBrush)));
        }
    }

    public sealed partial class MainWindow : Window
    {
        private AppWindow _appWindow;

        // private readonly IUserSettings _userSettings;
        public MockSettings Settings { get; } = new MockSettings();

        // this collections holds the keys currently being displayed
        private readonly ObservableCollection<KeyModel> ActiveKeys { get; } = new ObservableCollection<KeyModel>();

        public MainWindow()
        {
            InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;

            _appWindow = GetAppWindowForCurrentWindow();
            ConfigureAppWindow();

            _userSettings = App.GetService<IUserSettings>();

            // simulation of key presses for testing
            this.CoreWindow.KeyDown += (sender, args) =>
            {
                // This would be replaced by your global hook logic
                string keyText = e.VirtualKey.ToString();

                // Simple formatting
                if (keyText.StartsWith("Number")) keyText = keyText.Replace("Number", "");
                if (keyText == "Control") keyText = "Ctrl";

                ShowKey(keyText);
            };
        }

        private void ShowKey(string keyText)
        {
            var model = new KeyModel { Text = keyText };
            ActiveKeys.Add(model);

            // Use a DispatcherTimer to remove the key after the OverlayTimeout
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(Settings.OverlayTimeout)
            };
            timer.Tick += (s, e) =>
            {
                ActiveKeys.Remove(model);
                timer.Stop();
            };
            timer.Start();
        }

        // DRAGGING LOGIC
        private bool _isDragging;
        private PointInt32 _lastMousePos;

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

                // Move the window
                _appWindow.Move(new PointInt32(windowPos.X + deltaX, windowPos.Y + deltaY));

                // No need to update _lastMousePos, we're tracking offset from start
                // We're calculating delta from last *move* event, not start.
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
}
