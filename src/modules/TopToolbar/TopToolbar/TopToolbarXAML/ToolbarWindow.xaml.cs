// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Timers;
using Microsoft.UI.Composition;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Shapes;
using WinUIEx;
using Timer = System.Timers.Timer;

namespace TopToolbar
{
    public sealed partial class ToolbarWindow : WindowEx, IDisposable
    {
        private Timer _monitorTimer;
        private bool _isVisible;
        private const int TriggerZoneHeight = 2; // pixels from top edge
        private const int HiddenYOffset = -80; // shift above screen when hidden
        private IntPtr _hwnd;
        private bool _initializedLayout;
        private Border _toolbarContainer; // runtime-resolved toolbar root

        // Composition clipping cache
        private CompositionRoundedRectangleGeometry _geo;
        private CompositionGeometricClip _clip;

        public ToolbarWindow()
        {
            Title = "Top Toolbar";

            // Make window background completely transparent
            this.SystemBackdrop = new WinUIEx.TransparentTintBackdrop(
                Windows.UI.Color.FromArgb(0, 0, 0, 0));

            // Create the toolbar content programmatically with transparent root
            CreateToolbarContent();

            // Apply styles when content is loaded
            _toolbarContainer.Loaded += (s, e) =>
            {
                if (!_initializedLayout)
                {
                    _hwnd = this.GetWindowHandle();
                    ApplyTransparentBackground();
                    ApplyFramelessStyles();
                    ResizeToContent();
                    PositionAtTopCenter();
                    ApplyCapsuleClip(); // Apply smooth composition clipping
                    _initializedLayout = true;
                }
            };

            // Handle size and DPI changes
            this.AppWindow.Changed += (s, e) =>
            {
                if (e.DidSizeChange)
                {
                    UpdateClipGeometry();
                }
            };

            // Handle content size changes
            _toolbarContainer.SizeChanged += (s, e) =>
            {
                ResizeToContent();
                UpdateClipGeometry();
            };

            // Apply styles immediately after activation as backup
            this.Activated += (s, e) => MakeTopMost();

            StartMonitoring();
            HideToolbar(initial: true);
        }

        private void CreateToolbarContent()
        {
            // Create a completely transparent root container
            var rootGrid = new Grid
            {
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
            };

            // Create the toolbar content
            var border = new Border
            {
                Name = "ToolbarContainer",
                CornerRadius = new CornerRadius(36),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(245, 245, 245, 247)),
                Opacity = 0.96,
                Height = 64,
                Padding = new Thickness(24, 0, 24, 0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            var mainStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 32,
                VerticalAlignment = VerticalAlignment.Center,
            };

            // Group 1
            var group1 = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 16,
            };
            group1.Children.Add(CreateButton("🏠", "Home", OnHomeClick));
            group1.Children.Add(CreateButton("🔍", "Search", OnSearchClick));
            group1.Children.Add(CreateButton("📁", "Files", OnFilesClick));
            group1.Children.Add(CreateButton("🧮", "Calculator", OnCalcClick));
            mainStack.Children.Add(group1);

            // Separator 1
            mainStack.Children.Add(new Rectangle
            {
                Width = 1,
                Height = 32,
                Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(51, 0, 0, 0)),
                VerticalAlignment = VerticalAlignment.Center,
            });

            // Group 2
            var group2 = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 16,
            };
            group2.Children.Add(CreateButton("📷", "Camera", OnCameraClick));
            group2.Children.Add(CreateButton("🎵", "Music", OnMusicClick));
            group2.Children.Add(CreateButton("✉", "Mail", OnMailClick));
            group2.Children.Add(CreateButton("📅", "Calendar", OnCalendarClick));
            mainStack.Children.Add(group2);

            // Separator 2
            mainStack.Children.Add(new Rectangle
            {
                Width = 1,
                Height = 32,
                Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(51, 0, 0, 0)),
                VerticalAlignment = VerticalAlignment.Center,
            });

            // Group 3
            var group3 = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 16,
            };
            group3.Children.Add(CreateButton("💻", "Display", OnDisplayClick));
            group3.Children.Add(CreateButton("🔊", "Sound", OnSoundClick));
            group3.Children.Add(CreateButton("📶", "Network", OnNetworkClick));
            group3.Children.Add(CreateButton("⚙", "Settings", OnSettingsClick));
            mainStack.Children.Add(group3);

            border.Child = mainStack;
            rootGrid.Children.Add(border);
            this.Content = rootGrid;
            _toolbarContainer = border;
        }

        private Button CreateButton(string content, string tooltip, RoutedEventHandler clickHandler)
        {
            var button = new Button
            {
                Content = content,
                MinWidth = 40,
                MinHeight = 40,
            };

            Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(button, tooltip);
            button.Click += clickHandler;
            return button;
        }

        private void ResizeToContent()
        {
            if (_toolbarContainer != null)
            {
                // Get the actual size after layout
                double actualWidth = _toolbarContainer.ActualWidth;
                double actualHeight = _toolbarContainer.ActualHeight;

                if (actualWidth > 0 && actualHeight > 0)
                {
                    // Resize window to exactly fit the content (no extra margin)
                    int width = (int)Math.Ceiling(actualWidth);
                    int height = (int)Math.Ceiling(actualHeight);
                    this.AppWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
                }
            }
        }

        private void PositionAtTopCenter()
        {
            var displayArea = DisplayArea.GetFromWindowId(this.AppWindow.Id, DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;
            int width = this.AppWindow.Size.Width;
            int height = this.AppWindow.Size.Height;
            int x = workArea.X + ((workArea.Width - width) / 2);
            int y = workArea.Y - height; // hidden above top
            this.AppWindow.Move(new Windows.Graphics.PointInt32(x, y));
        }

        private void StartMonitoring()
        {
            _monitorTimer = new Timer(120);
            _monitorTimer.Elapsed += MonitorTimer_Elapsed;
            _monitorTimer.AutoReset = true;
            _monitorTimer.Start();
        }

        private void MonitorTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var pt = GetCursorPos();
            var displayArea = DisplayArea.GetFromPoint(new Windows.Graphics.PointInt32(pt.X, pt.Y), DisplayAreaFallback.Primary);
            var topEdge = displayArea.WorkArea.Y;
            bool inTrigger = pt.Y <= topEdge + TriggerZoneHeight;

            if (inTrigger && !_isVisible)
            {
                DispatcherQueue.TryEnqueue(() => ShowToolbar());
            }
            else if (!inTrigger && _isVisible)
            {
                // hide only if mouse left the bar area
                DispatcherQueue.TryEnqueue(() =>
                {
                    var relativeY = pt.Y - this.AppWindow.Position.Y;

                    // left below the bar or above top
                    if (relativeY > 80 || pt.Y < topEdge)
                    {
                        HideToolbar();
                    }
                });
            }
        }

        private void ShowToolbar()
        {
            _isVisible = true;
            var pos = this.AppWindow.Position;

            // Slide down animation (simple incremental move)
            int targetY = 0;
            this.AppWindow.Move(new Windows.Graphics.PointInt32(pos.X, targetY));
        }

        private void HideToolbar(bool initial = false)
        {
            _isVisible = false;
            var pos = this.AppWindow.Position;
            int y = -(int)this.AppWindow.Size.Height + HiddenYOffset;
            this.AppWindow.Move(new Windows.Graphics.PointInt32(pos.X, y));
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            // keep always on top
            MakeTopMost();
        }

        private void OnHomeClick(object sender, RoutedEventArgs e)
        {
        }

        private void OnSearchClick(object sender, RoutedEventArgs e)
        {
        }

        private void OnFilesClick(object sender, RoutedEventArgs e)
        {
        }

        private void OnCalcClick(object sender, RoutedEventArgs e)
        {
        }

        private void OnCameraClick(object sender, RoutedEventArgs e)
        {
        }

        private void OnMusicClick(object sender, RoutedEventArgs e)
        {
        }

        private void OnMailClick(object sender, RoutedEventArgs e)
        {
        }

        private void OnCalendarClick(object sender, RoutedEventArgs e)
        {
        }

        private void OnDisplayClick(object sender, RoutedEventArgs e)
        {
        }

        private void OnSoundClick(object sender, RoutedEventArgs e)
        {
        }

        private void OnNetworkClick(object sender, RoutedEventArgs e)
        {
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private static POINT GetCursorPos()
        {
            GetCursorPos(out var p);
            return p;
        }

        // P/Invoke to keep window topmost if WinUIEx helper not available
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HwndTopMost = new IntPtr(-1);
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpShowWindow = 0x0040;

        private void MakeTopMost()
        {
            var handle = _hwnd != IntPtr.Zero ? _hwnd : this.GetWindowHandle();
            SetWindowPos(handle, HwndTopMost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
        }

        private void ApplyFramelessStyles()
        {
            // Remove caption / border styles so only the toolbar content is visible
            const int GWL_STYLE = -16;
            const int GWL_EXSTYLE = -20;
            const int WS_CAPTION = 0x00C00000;
            const int WS_THICKFRAME = 0x00040000;
            const int WS_MINIMIZEBOX = 0x00020000;
            const int WS_MAXIMIZEBOX = 0x00010000;
            const int WS_SYSMENU = 0x00080000;
            const int WS_POPUP = unchecked((int)0x80000000);
            const int WS_VISIBLE = 0x10000000;
            const int WS_EX_TOOLWINDOW = 0x00000080;
            const int WS_EX_TOPMOST = 0x00000008;

            var h = _hwnd;
            int style = GetWindowLong(h, GWL_STYLE);
            style &= ~(WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU);
            style |= WS_POPUP | WS_VISIBLE;
            _ = SetWindowLong(h, GWL_STYLE, style);

            int exStyle = GetWindowLong(h, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
            _ = SetWindowLong(h, GWL_EXSTYLE, exStyle);
        }

        private void ApplyTransparentBackground()
        {
            // The key is to NOT have any background on the window itself
            // With WS_EX_LAYERED + no background, only visible content shows
        }

        private void ApplyCapsuleClip()
        {
            var root = (FrameworkElement)this.Content;
            if (root == null)
            {
                return;
            }

            var visual = ElementCompositionPreview.GetElementVisual(root);
            var comp = visual.Compositor;

            _geo ??= comp.CreateRoundedRectangleGeometry();
            _clip ??= comp.CreateGeometricClip(_geo);
            visual.Clip = _clip;

            UpdateClipGeometry();
        }

        private void UpdateClipGeometry()
        {
            if (_hwnd == IntPtr.Zero || _geo == null || _toolbarContainer == null)
            {
                return;
            }

            double scale = GetDpiForWindow(_hwnd) / 96.0;
            float w = (float)Math.Round(this.AppWindow.Size.Width * scale) / (float)scale;
            float h = (float)Math.Round(this.AppWindow.Size.Height * scale) / (float)scale;
            float r = (float)Math.Round(_toolbarContainer.CornerRadius.TopLeft * scale) / (float)scale;

            _geo.Size = new Vector2(w, h);
            _geo.CornerRadius = new Vector2(r);
        }

        private static bool PointInRoundedRect(float x, float y, float w, float h, float r)
        {
            // Check if point is in main rectangles (excluding corners)
            if (x >= r && x <= w - r && y >= 0 && y <= h)
            {
                return true;
            }

            if (y >= r && y <= h - r && x >= 0 && x <= w)
            {
                return true;
            }

            // Check if point is in any of the corner circles
            Vector2[] corners = { new Vector2(r, r), new Vector2(w - r, r), new Vector2(r, h - r), new Vector2(w - r, h - r) };
            foreach (var corner in corners)
            {
                float dx = x - corner.X;
                float dy = y - corner.Y;
                if ((dx * dx) + (dy * dy) <= (r * r))
                {
                    return true;
                }
            }

            return false;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        public void Dispose()
        {
            _monitorTimer?.Stop();
            _monitorTimer?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
