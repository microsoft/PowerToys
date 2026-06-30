// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Common.UI;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using ShortcutGuide.Helpers;
using ShortcutGuide.Models;
using ShortcutGuide.Pages;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;
using Windows.UI.WindowManagement;
using WinRT.Interop;
using WinUIEx;
using WinUIEx.Messaging;

namespace ShortcutGuide
{
    public sealed partial class MainWindow : WindowEx, IDisposable
    {
        private readonly Stopwatch _sessionStopwatch = Stopwatch.StartNew();
        private readonly Task<Dictionary<string, string?>> _getAppIdsTask;
        private Dictionary<string, string?> _currentApplicationIds = [];
        private ShortcutFile? _shortcutFile;
        private string _selectedAppName = null!;
        private string _closeType = "Unknown";

        internal long SessionDurationMs => _sessionStopwatch.ElapsedMilliseconds;

        internal string CloseType => _closeType;

        private bool _setPosition;

        public MainWindow()
        {
            this.InitializeComponent();

            _getAppIdsTask = Task.Run(() =>
            {
                Program.CopyAndIndexGenerationThread.Join();
                _currentApplicationIds = ManifestInterpreter.GetAllCurrentApplicationIds(Program.ForegroundWindowHandle);
                return _currentApplicationIds;
            });

            Title = ResourceLoaderInstance.ResourceLoader.GetString("Title")!;
            ExtendsContentIntoTitleBar = true;

#if !DEBUG
            this.SetIsAlwaysOnTop(true);
            this.SetIsShownInSwitchers(false);
#endif
            WindowMessageMonitor msgMonitor = new(this);
            msgMonitor.WindowMessageReceived += (_, e) =>
            {
                const int WM_NCLBUTTONDBLCLK = 0x00A3;
                if (e.Message.MessageId == WM_NCLBUTTONDBLCLK)
                {
                    // Disable double click on title bar to maximize window
                    e.Result = 0;
                    e.Handled = true;
                }
            };

            Activated += Window_Activated;

            Content.KeyUp += (_, e) =>
            {
                if (e.Key == VirtualKey.Escape)
                {
                    _closeType = "Escape";
                    Close();
                }
            };

            switch (App.ShortcutGuideProperties.Theme.Value)
            {
                case "dark":
                    ((FrameworkElement)Content).RequestedTheme = ElementTheme.Dark;
                    this.MainPage.RequestedTheme = ElementTheme.Dark;
                    break;
                case "light":
                    ((FrameworkElement)Content).RequestedTheme = ElementTheme.Light;
                    this.MainPage.RequestedTheme = ElementTheme.Light;
                    break;
                case "system":
                    // Ignore, as the theme will be set by the system.
                    break;
                default:
                    Logger.LogError("Invalid theme value in settings: " + App.ShortcutGuideProperties.Theme.Value);
                    break;
            }
        }

        protected override void OnStateChanged(WindowState state)
        {
            if (state == WindowState.Maximized)
            {
                this.SetWindowPosition();
            }
        }

        protected override void OnPositionChanged(PointInt32 position)
        {
            this.SetWindowPosition();
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState == WindowActivationState.Deactivated && !this._taskBarWindowActivated)
            {
#if !DEBUG
                _closeType = "Deactivated";
                Close();
#endif
            }

            if (this._taskBarWindowActivated)
            {
                this._taskBarWindowActivated = false;
                this.BringToFront();
            }

            // The code below sets the position of the window to the center of the monitor, but only if it hasn't been set before.
            if (!this._setPosition)
            {
                this.SetWindowPosition();
                this._setPosition = true;

                AppWindow.Changed += (_, a) =>
                {
                    if (!a.DidPresenterChange)
                    {
                        return;
                    }

                    this.SetWindowPosition();
                };
            }

            _ = this.InitializeNavItemsAsync();
        }

        private async Task InitializeNavItemsAsync()
        {
            try
            {
                _currentApplicationIds = await _getAppIdsTask.ConfigureAwait(true);
                this.SetNavItems();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize navigation items.", ex);
                _closeType = "InitializationFailed";
                this.DispatcherQueue.TryEnqueue(() => this.Close());
            }
        }

        private void SetNavItems()
        {
            // Populate the window selector with the current application IDs if it is empty.
            // TO DO: Check if Settings button is considered an item too.
            if (this.WindowSelector.MenuItems.Count == 0)
            {
                string defaultShellName = ManifestInterpreter.GetCachedIndexYamlFile().DefaultShellName;

                foreach (var (item, executablePath) in this._currentApplicationIds)
                {
                    if (item == defaultShellName)
                    {
                        var pathData = (string)Application.Current.Resources["WindowsLogoPathData"];
                        this.WindowSelector.MenuItems.Add(new NavigationViewItem { Name = item, Content = "Windows", Icon = CreatePathIcon(pathData) });
                    }
                    else if (item == "Microsoft.PowerToys")
                    {
                        var pathData = (string)Application.Current.Resources["PowerToysLogoPathData"];
                        this.WindowSelector.MenuItems.Add(new NavigationViewItem { Name = item, Content = ManifestInterpreter.GetShortcutsOfApplication(item).Name, Icon = CreatePathIcon(pathData) });
                    }
                    else
                    {
                        try
                        {
                            IconElement icon = BuildNavIcon(executablePath);
                            this.WindowSelector.MenuItems.Add(new NavigationViewItem { Name = item, Content = ManifestInterpreter.GetShortcutsOfApplication(item).Name, Icon = icon });
                        }
                        catch (IOException ex)
                        {
                            Logger.LogError($"Failed to build nav item for application '{item}' (executable '{executablePath}').", ex);
                        }
                    }
                }

                if (this.WindowSelector.MenuItems.Count > 0)
                {
                    this.WindowSelector.SelectedItem = this.WindowSelector.MenuItems[0];
                }
            }
        }

        private static IconElement BuildNavIcon(string? executablePath)
        {
            BitmapImage? bitmap = IconHelper.TryGetExecutableIcon(executablePath);
            if (bitmap is not null)
            {
                return new ImageIcon { Source = bitmap };
            }

            return new FontIcon { Glyph = "\uEB91" };
        }

        private static PathIcon CreatePathIcon(string pathData)
        {
            var geometry = (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), pathData);
            return new PathIcon
            {
                Data = geometry,
                Width = 20,
                Height = 20,
            };
        }

        private bool _hasMovedToRightMonitor;

        private void SetWindowPosition()
        {
            try
            {
                if (!this._hasMovedToRightMonitor)
                {
                    NativeMethods.GetCursorPos(out NativeMethods.POINT lpPoint);
                    AppWindow.Move(new NativeMethods.POINT { Y = lpPoint.Y - ((int)Height / 2), X = lpPoint.X - ((int)Width / 2) });
                    this._hasMovedToRightMonitor = true;
                }

                var hwnd = WindowNative.GetWindowHandle(this);
                float dpi = DpiHelper.GetDPIScaleForWindow(hwnd);
                Rect monitorRect = DisplayHelper.GetWorkAreaForDisplayWithWindow(hwnd);

                var windowPosition = (ShortcutGuideWindowPosition)App.ShortcutGuideProperties.WindowPosition.Value;

                // App.TaskBarWindow / its AppWindow can briefly be null during the reentrant
                // Hide → Activate → BringToFront chain triggered from SelectionChanged. When the
                // taskbar window is not currently observable, skip the overlap adjustment instead
                // of crashing the overlay (issue #48448).
                var taskbarWindow = App.TaskBarWindow?.AppWindow;
                bool taskbarOnLeft = false;
                bool taskbarOnRight = false;
                if (taskbarWindow is not null)
                {
                    taskbarOnLeft = taskbarWindow.IsVisible && taskbarWindow.Position.X < AppWindow.Position.X + Width && windowPosition == ShortcutGuideWindowPosition.Left;
                    taskbarOnRight = taskbarWindow.IsVisible && taskbarWindow.Position.X + taskbarWindow.Size.Width > AppWindow.Position.X && windowPosition == ShortcutGuideWindowPosition.Right;
                }

                double newHeight = monitorRect.Height / dpi;
                if (taskbarWindow is not null && (taskbarOnLeft || taskbarOnRight))
                {
                    newHeight -= taskbarWindow.Size.Height;
                }

                MaxHeight = newHeight;
                MinHeight = newHeight;
                Height = newHeight;

                int xPosition = windowPosition == ShortcutGuideWindowPosition.Right
                    ? (int)(monitorRect.X + monitorRect.Width) - (int)(Width * dpi)
                    : (int)monitorRect.X;

                this.MoveAndResize(xPosition, (int)monitorRect.Y, Width, Height);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to set Shortcut Guide window position; keeping previous layout.", ex);
            }
        }

        /// <summary>
        /// Tracks whether the taskbar window was activated. So that the main window does not close.
        /// </summary>
        private bool _taskBarWindowActivated;

        private void WindowSelector_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is not NavigationViewItem selectedItem)
            {
                return;
            }

            try
            {
                this._selectedAppName = selectedItem.Name;
                App.CurrentAppName = this._selectedAppName;
                this._shortcutFile = ManifestInterpreter.GetShortcutsOfApplication(this._selectedAppName);

                App.TaskBarWindow?.Hide();
                if (this._shortcutFile is ShortcutFile file)
                {
                    // Show the taskbar button window only when the selected app exposes the <TASKBAR1-9> section.
                    if (file.Shortcuts is not null && file.Shortcuts.Any(c => c.SectionName?.StartsWith("<TASKBAR1-9>", StringComparison.Ordinal) == true))
                    {
                        this._taskBarWindowActivated = true;
                        App.TaskBarWindow?.Activate();
                    }

                    // Reposition before navigating so the taskbar window does not clip into the main window.
                    this.SetWindowPosition();
                    this.ContentFrame.Navigate(
                        typeof(ShortcutsPage),
                        new ShortcutPageNavParam { ShortcutFile = file, AppName = this._selectedAppName });
                }
            }
            catch (Exception ex)
            {
                // Guard against exceptions during section navigation so the overlay does not close on the user.
                // InitializeNavItemsAsync's catch interprets any exception bubbling out of the initial
                // SelectedItem assignment as a fatal init failure and closes the window (issue #48448).
                Logger.LogError($"Failed to handle Shortcut Guide section selection '{selectedItem.Name}'.", ex);
            }
        }

        private void Settings_Tapped(object sender, TappedRoutedEventArgs e)
        {
            SettingsDeepLink.OpenSettings(SettingsDeepLink.SettingsWindow.ShortcutGuide);
        }

        public void Dispose()
        {
            _getAppIdsTask.Dispose();
        }
    }
}
