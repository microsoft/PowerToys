// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Common.UI;
using ManagedCommon;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
    public sealed partial class MainWindow : WindowEx
    {
        private readonly Dictionary<string, string?> _currentApplicationIds;
        private ShortcutFile? _shortcutFile;
        private string _selectedAppName = null!;

        private bool _setPosition;

        public MainWindow()
        {
            this._currentApplicationIds = ManifestInterpreter.GetAllCurrentApplicationIds();

            this.InitializeComponent();

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
                Content.GettingFocus += (_, _) =>
                {
                    this.FakeSettingsButton.Height = 10;
                    this.FakeSettingsButton.Height = 0;
                };

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

            this.SetNavItems();
        }

        private void SetNavItems()
        {
            // Populate the window selector with the current application IDs if it is empty.
            // TO DO: Check if Settings button is considered an item too.
            if (this.WindowSelector.MenuItems.Count == 0)
            {
                string defaultShellName = ManifestInterpreter.GetIndexYamlFile().DefaultShellName;

                foreach (var (item, executablePath) in this._currentApplicationIds)
                {
                    if (item == defaultShellName)
                    {
                        this.WindowSelector.MenuItems.Add(new NavigationViewItem { Name = item, Content = "Windows", Icon = new FontIcon() { Glyph = "\xE770" } });
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

                this.WindowSelector.SelectedItem = this.WindowSelector.MenuItems[0];
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

        private bool _hasMovedToRightMonitor;

        private void SetWindowPosition()
        {
            if (!this._hasMovedToRightMonitor)
            {
                NativeMethods.GetCursorPos(out NativeMethods.POINT lpPoint);
                AppWindow.Move(new NativeMethods.POINT { Y = lpPoint.Y - ((int)Height / 2), X = lpPoint.X - ((int)Width / 2) });
                this._hasMovedToRightMonitor = true;
            }

            var hwnd = WindowNative.GetWindowHandle(this);
            float dpi = DpiHelper.GetDPIScaleForWindow(hwnd.ToInt32());
            Rect monitorRect = DisplayHelper.GetWorkAreaForDisplayWithWindow(hwnd);

            string windowPosition = App.ShortcutGuideProperties.WindowPosition.Value;
            var taskbarWindow = App.TaskBarWindow.AppWindow;
            bool taskbarOnLeft = taskbarWindow.IsVisible && taskbarWindow.Position.X < AppWindow.Position.X + Width && windowPosition == "left";
            bool taskbarOnRight = taskbarWindow.IsVisible && taskbarWindow.Position.X + taskbarWindow.Size.Width > AppWindow.Position.X && windowPosition == "right";

            double newHeight = monitorRect.Height / dpi;
            if (taskbarOnLeft || taskbarOnRight)
            {
                newHeight -= taskbarWindow.Size.Height;
            }

            MaxHeight = newHeight;
            MinHeight = newHeight;
            Height = newHeight;

            int xPosition = windowPosition == "right"
                ? (int)(monitorRect.X + monitorRect.Width) - (int)(Width * dpi)
                : (int)monitorRect.X;

            this.MoveAndResize(xPosition, (int)monitorRect.Y, Width, Height);
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

            this._selectedAppName = selectedItem.Name;
            App.CurrentAppName = this._selectedAppName;
            this._shortcutFile = ManifestInterpreter.GetShortcutsOfApplication(this._selectedAppName);

            App.TaskBarWindow.Hide();
            if (this._shortcutFile is ShortcutFile file)
            {
                // Show the taskbar button window only when the selected app exposes the <TASKBAR1-9> section.
                if (file.Shortcuts is not null && file.Shortcuts.Any(c => c.SectionName == "<TASKBAR1-9>"))
                {
                    this._taskBarWindowActivated = true;
                    App.TaskBarWindow.Activate();
                }

                // Reposition before navigating so the taskbar window does not clip into the main window.
                this.SetWindowPosition();
                this.ContentFrame.Navigate(
                    typeof(ShortcutsPage),
                    new ShortcutPageNavParam { ShortcutFile = file, AppName = this._selectedAppName });
            }
        }

        private void Settings_Tapped(object sender, TappedRoutedEventArgs e)
        {
            SettingsDeepLink.OpenSettings(SettingsDeepLink.SettingsWindow.ShortcutGuide);
        }
    }
}
