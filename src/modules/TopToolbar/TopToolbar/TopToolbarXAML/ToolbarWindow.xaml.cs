// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Timers;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using TopToolbar.Services;
using TopToolbar.ViewModels;
using WinUIEx;
using Path = System.IO.Path;
using Timer = System.Timers.Timer;

namespace TopToolbar
{
    public sealed partial class ToolbarWindow : WindowEx, IDisposable
    {
        private const int TriggerZoneHeight = 2;
        private readonly ToolbarViewModel _vm = new(new ToolbarConfigService());
        private Timer _monitorTimer;
        private Timer _configWatcherDebounce;
        private bool _isVisible;
        private bool _builtConfigOnce;
        private IntPtr _hwnd;
        private bool _initializedLayout;
        private Border _toolbarContainer;
        private ScrollViewer _scrollViewer;
        private FileSystemWatcher _configWatcher;

        public ToolbarWindow()
        {
            Title = "Top Toolbar";

            // Make window background completely transparent
            this.SystemBackdrop = new WinUIEx.TransparentTintBackdrop(
                Windows.UI.Color.FromArgb(0, 0, 0, 0));

            // Create the toolbar content programmatically with transparent root
            CreateToolbarShell();

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
                    AppWindow.Hide();
                    _isVisible = false;
                    _initializedLayout = true;
                }
            };

            // Apply styles immediately after activation as backup
            this.Activated += (s, e) => MakeTopMost();

            StartMonitoring();
            StartWatchingConfig();

            // Load config and build UI when window activates
            this.Activated += async (s, e) =>
            {
                if (_builtConfigOnce)
                {
                    return;
                }

                await _vm.LoadAsync(this.DispatcherQueue);

                // Ensure UI-thread access for XAML object tree
                DispatcherQueue.TryEnqueue(() =>
                {
                    BuildToolbarFromConfig();
                    ResizeToContent();
                    PositionAtTopCenter();
                    _builtConfigOnce = true;
                });
            };
        }

        private void CreateToolbarShell()
        {
            // Create a completely transparent root container with optimized rendering
            var rootGrid = new Grid
            {
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                UseLayoutRounding = true,
                IsHitTestVisible = true,
            };

            // Create the toolbar content with modern macOS-style design
            var border = new Border
            {
                Name = "ToolbarContainer",
                CornerRadius = new CornerRadius(12),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 255, 255, 255)),
                Height = 48,
                Padding = new Thickness(12, 6, 12, 6),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                UseLayoutRounding = true,
                IsHitTestVisible = true,     // the pill remains interactive

                // Optional: ThemeShadow may need a proper shadow host; keep or remove as you like.
                Shadow = new Microsoft.UI.Xaml.Media.ThemeShadow(),
            };

            var mainStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                IsHitTestVisible = true,
                Name = "MainStack",
            };

            _scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollMode = Microsoft.UI.Xaml.Controls.ScrollMode.Enabled,
                VerticalScrollMode = Microsoft.UI.Xaml.Controls.ScrollMode.Disabled,
                Content = mainStack,
            };

            border.Child = _scrollViewer;
            rootGrid.Children.Add(border);
            this.Content = rootGrid;
            _toolbarContainer = border;
        }

        private void StartWatchingConfig()
        {
            try
            {
                var service = new ToolbarConfigService();
                var path = service.ConfigPath;
                var dir = Path.GetDirectoryName(path);
                var file = Path.GetFileName(path);
                if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(file))
                {
                    return;
                }

                _configWatcherDebounce = new Timer(250) { AutoReset = false };
                _configWatcherDebounce.Elapsed += async (s, e) =>
                {
                    await _vm.LoadAsync(this.DispatcherQueue);
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        BuildToolbarFromConfig();
                        ResizeToContent();
                    });
                };

                _configWatcher = new FileSystemWatcher(dir, file)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true,
                };

                FileSystemEventHandler onChanged = (s, e) =>
                {
                    _configWatcherDebounce.Stop();
                    _configWatcherDebounce.Start();
                };
                RenamedEventHandler onRenamed = (s, e) =>
                {
                    _configWatcherDebounce.Stop();
                    _configWatcherDebounce.Start();
                };

                _configWatcher.Changed += onChanged;
                _configWatcher.Created += onChanged;
                _configWatcher.Deleted += onChanged;
                _configWatcher.Renamed += onRenamed;
            }
            catch
            {
                // ignore watcher failures
            }
        }

        private void BuildToolbarFromConfig()
        {
            StackPanel mainStack = (_toolbarContainer?.Child as ScrollViewer)?.Content as StackPanel
                                   ?? _toolbarContainer?.Child as StackPanel;
            if (mainStack == null)
            {
                return;
            }

            mainStack.Children.Clear();

            // Use only groups that have at least one enabled button
            var nonEmptyGroups = _vm.Groups
                .Where(g => g.IsEnabled)
                .Select(g => new { Group = g, EnabledButtons = g.Buttons.Where(b => b.IsEnabled).ToList() })
                .Where(x => x.EnabledButtons.Count > 0)
                .ToList();

            for (int gi = 0; gi < nonEmptyGroups.Count; gi++)
            {
                var group = nonEmptyGroups[gi].Group;
                var enabledButtons = nonEmptyGroups[gi].EnabledButtons;
                var groupPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };

                foreach (var btn in enabledButtons)
                {
                    var iconButton = CreateIconButton(btn);
                    groupPanel.Children.Add(iconButton);
                }

                mainStack.Children.Add(groupPanel);

                // Add separator only between non-empty groups
                if (gi != nonEmptyGroups.Count - 1)
                {
                    mainStack.Children.Add(new Rectangle
                    {
                        Width = 1,
                        Height = 24,
                        Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(40, 0, 0, 0)),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(8, 0, 8, 0),
                        IsHitTestVisible = false,
                    });
                }
            }

            // Settings button at far right
            mainStack.Children.Add(new Rectangle
            {
                Width = 1,
                Height = 24,
                Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(40, 0, 0, 0)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0),
                IsHitTestVisible = false,
            });

            var settingsButton = CreateIconButton("\uE713", "Toolbar Settings", (s, e) =>
            {
                var win = new SettingsWindow();
                win.AppWindow.Move(new Windows.Graphics.PointInt32(this.AppWindow.Position.X + 50, this.AppWindow.Position.Y + 60));
                win.Activate();
            });
            mainStack.Children.Add(settingsButton);
        }

        private Button CreateIconButton(string content, string tooltip, RoutedEventHandler clickHandler)
        {
            var button = new Button
            {
                Content = new FontIcon { Glyph = content, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"), FontSize = 16 },
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(6),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)), // Transparent base
                BorderBrush = null,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2),
                Padding = new Thickness(0),
                UseLayoutRounding = true,
            };

            // Use WinUI button visual state resources to ensure stable hover/pressed visuals
            var hoverBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(60, 0, 0, 0));
            var pressedBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(100, 0, 0, 0));
            var normalBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            // Override per-button theme resources so the default template keeps our visuals
            button.Resources["ButtonBackground"] = normalBrush;
            button.Resources["ButtonBackgroundPointerOver"] = hoverBrush;
            button.Resources["ButtonBackgroundPressed"] = pressedBrush;
            button.Resources["ButtonBackgroundDisabled"] = normalBrush;

            Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(button, tooltip);
            button.Click += clickHandler;
            return button;
        }

        private Button CreateIconButton(TopToolbar.Models.ToolbarButton model)
        {
            RoutedEventHandler handler = (s, e) => ToolbarActionExecutor.Execute(model.Action);
            var button = new Button
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(6),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
                BorderBrush = null,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2),
                Padding = new Thickness(0),
                UseLayoutRounding = true,
            };

            // Visual states for hover/press
            var hoverBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(60, 0, 0, 0));
            var pressedBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(100, 0, 0, 0));
            var normalBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            button.Resources["ButtonBackground"] = normalBrush;
            button.Resources["ButtonBackgroundPointerOver"] = hoverBrush;
            button.Resources["ButtonBackgroundPressed"] = pressedBrush;
            button.Resources["ButtonBackgroundDisabled"] = normalBrush;

            // Content based on icon type
            if (model.IconType == TopToolbar.Models.ToolbarIconType.Image && !string.IsNullOrWhiteSpace(model.IconPath))
            {
                try
                {
                    Uri imgUri = null;
                    var rawPath = model.IconPath;
                    if (System.IO.File.Exists(rawPath))
                    {
                        // Build cache-busting file URI with last write ticks
                        var ver = System.IO.File.GetLastWriteTimeUtc(rawPath).Ticks;
                        var ub = new UriBuilder
                        {
                            Scheme = "file",
                            Path = rawPath,
                            Query = $"v={ver}",
                        };
                        imgUri = ub.Uri;
                        ManagedCommon.Logger.LogInfo($"Toolbar image: local path exists '{rawPath}', uri='{imgUri}'");
                    }
                    else if (Uri.TryCreate(rawPath, UriKind.Absolute, out var parsed))
                    {
                        imgUri = parsed;
                        ManagedCommon.Logger.LogInfo($"Toolbar image: using provided URI '{parsed}'");
                    }
                    else
                    {
                        // Best effort: prefix file scheme for non-existent yet absolute-looking paths
                        var prefixed = new UriBuilder { Scheme = "file", Path = rawPath }.Uri;
                        imgUri = prefixed;
                        ManagedCommon.Logger.LogInfo($"Toolbar image: prefixed file URI '{prefixed}'");
                    }

                    var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                    bmp.UriSource = imgUri;
                    var img = new Image
                    {
                        Width = 16,
                        Height = 16,
                        Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
                        Source = bmp,
                    };
                    button.Content = img;
                }
                catch
                {
                    // Fallback to glyph if loading fails
                    ManagedCommon.Logger.LogWarning($"Toolbar image: failed to load '{model.IconPath}', fallback to glyph");
                    button.Content = new FontIcon { Glyph = model.IconGlyph, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"), FontSize = 16 };
                }
            }
            else
            {
                button.Content = new FontIcon { Glyph = model.IconGlyph, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"), FontSize = 16 };
            }

            Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(button, model.Name);
            button.Click += handler;
            return button;
        }

        private void ResizeToContent()
        {
            if (_toolbarContainer != null)
            {
                // Measure content desired width independent of current constraints
                StackPanel mainStack = (_toolbarContainer.Child as ScrollViewer)?.Content as StackPanel
                                       ?? _toolbarContainer.Child as StackPanel;
                if (mainStack == null)
                {
                    return;
                }

                mainStack.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
                double desiredWidth = mainStack.DesiredSize.Width + _toolbarContainer.Padding.Left + _toolbarContainer.Padding.Right;
                double desiredHeight = _toolbarContainer.ActualHeight > 0 ? _toolbarContainer.ActualHeight : 48;

                var displayArea = DisplayArea.GetFromWindowId(this.AppWindow.Id, DisplayAreaFallback.Primary);
                double maxWidth = displayArea.WorkArea.Width / 2.0;
                double widthToSet = Math.Min(desiredWidth, maxWidth);

                int width = (int)Math.Ceiling(widthToSet);
                int height = (int)Math.Ceiling(desiredHeight);

                this.AppWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
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
            GetCursorPos(out var pt);
            var displayArea = DisplayArea.GetFromPoint(new Windows.Graphics.PointInt32(pt.X, pt.Y), DisplayAreaFallback.Primary);
            var topEdge = displayArea.WorkArea.Y;
            bool inTrigger = pt.Y <= topEdge + TriggerZoneHeight;

            if (inTrigger && !_isVisible)
            {
                DispatcherQueue.TryEnqueue(() => ShowToolbar());
            }
            else if (!inTrigger && _isVisible)
            {
                // hide when cursor is not over the toolbar rectangle
                DispatcherQueue.TryEnqueue(() =>
                {
                    var winPos = this.AppWindow.Position;
                    var winSize = this.AppWindow.Size;
                    bool overToolbar = pt.X >= winPos.X && pt.X <= winPos.X + winSize.Width &&
                                       pt.Y >= winPos.Y && pt.Y <= winPos.Y + winSize.Height;
                    if (!overToolbar)
                    {
                        HideToolbar();
                    }
                });
            }
        }

        private void ShowToolbar()
        {
            _isVisible = true;

            // Reposition to current monitor top edge
            GetCursorPos(out var pt);
            var da = DisplayArea.GetFromPoint(new Windows.Graphics.PointInt32(pt.X, pt.Y), DisplayAreaFallback.Primary);
            var work = da.WorkArea;
            var size = AppWindow.Size;
            int x = work.X + ((work.Width - size.Width) / 2);
            int y = work.Y; // flush with top

            AppWindow.Move(new Windows.Graphics.PointInt32(x, y));
            AppWindow.Show(false); // show without activation
            MakeTopMost();
        }

        private void HideToolbar(bool initial = false)
        {
            _isVisible = false;
            AppWindow.Hide();
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
        public struct POINT
        {
            public int X;
            public int Y;
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
            _configWatcherDebounce?.Stop();
            _configWatcherDebounce?.Dispose();
            if (_configWatcher != null)
            {
                _configWatcher.EnableRaisingEvents = false;
                _configWatcher.Dispose();
            }

            GC.SuppressFinalize(this);
        }
    }
}
