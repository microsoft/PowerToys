// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Common.UI;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ShortcutGuide.Helpers;
using ShortcutGuide.Models;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;
using WinUIEx;
using static ShortcutGuide.NativeMethods;

namespace ShortcutGuide
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : WindowEx
    {
        private readonly string[] _currentApplicationIds;
        private readonly bool _firstRun;

        public static nint WindowHwnd { get; set; }

        private AppWindow _appWindow;
        private bool _setPosition;

        public MainWindow()
        {
            _currentApplicationIds = ManifestInterpreter.GetAllCurrentApplicationIds();

            InitializeComponent();

            Title = ResourceLoaderInstance.ResourceLoader.GetString("Title")!;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowHwnd = hwnd;
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);
#if !DEBUG
            this.SetIsAlwaysOnTop(true);
            this.SetIsShownInSwitchers(false);
#endif
            IsTitleBarVisible = false;
            ExtendsContentIntoTitleBar = true;

            Activated += Window_Activated;

            SettingsUtils settingsUtils = new();

            if (settingsUtils.SettingsExists(ShortcutGuideSettings.ModuleName, "Pinned.json"))
            {
                string pinnedPath = settingsUtils.GetSettingsFilePath(ShortcutGuideSettings.ModuleName, "Pinned.json");
                ShortcutPageParameters.PinnedShortcuts = JsonSerializer.Deserialize<Dictionary<string, List<ShortcutEntry>>>(File.ReadAllText(pinnedPath))!;
            }

            Content.KeyUp += (_, e) =>
            {
                if (e.Key == VirtualKey.Escape)
                {
                    Close();
                }
            };

            ShortcutGuideSettings shortcutGuideSettings = SettingsRepository<ShortcutGuideSettings>.GetInstance(settingsUtils).SettingsConfig;
            ShortcutGuideProperties shortcutGuideProperties = shortcutGuideSettings.Properties;

            switch (shortcutGuideProperties.Theme.Value)
            {
                case "dark":
                    ((FrameworkElement)Content).RequestedTheme = ElementTheme.Dark;
                    MainPage.RequestedTheme = ElementTheme.Dark;
                    MainPage.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0));
                    break;
                case "light":
                    ((FrameworkElement)Content).RequestedTheme = ElementTheme.Light;
                    MainPage.RequestedTheme = ElementTheme.Light;
                    MainPage.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
                    break;
                case "system":
                    // Ignore, as the theme will be set by the system.
                    break;
                default:
                    Logger.LogError("Invalid theme value in settings: " + shortcutGuideProperties.Theme.Value);
                    break;
            }

            _firstRun = shortcutGuideProperties.FirstRun.Value;
            shortcutGuideProperties.FirstRun = new BoolProperty(false);
#pragma warning disable CA1869 // Cache and reuse 'JsonSerializerOptions' instances
            settingsUtils.SaveSettings(JsonSerializer.Serialize(shortcutGuideSettings, new JsonSerializerOptions { WriteIndented = true }), "Shortcut Guide");
#pragma warning restore CA1869 // Cache and reuse 'JsonSerializerOptions' instances
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState == WindowActivationState.Deactivated)
            {
#if !DEBUG
                Close();
#endif
            }

            // The code below sets the position of the window to the center of the monitor, but only if it hasn't been set before.
            if (!_setPosition)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);

                _appWindow = AppWindow.GetFromWindowId(windowId);

                float dpiScale = DpiHelper.GetDPIScaleForWindow((int)hwnd);

                Rect monitorRect = DisplayHelper.GetWorkAreaForDisplayWithWindow(hwnd);

                // Set window to 600 pixels width and full monitor height
                this.SetWindowSize(600 * dpiScale, monitorRect.Height / dpiScale);

                // Position window at the left edge of the monitor
                _appWindow.Move(new PointInt32((int)monitorRect.X, (int)monitorRect.Y));
                _setPosition = true;

                AppWindow.Changed += (_, a) =>
                {
                    if (!a.DidPresenterChange)
                    {
                        return;
                    }

                    Rect monitorRect = DisplayHelper.GetWorkAreaForDisplayWithWindow(hwnd);
                    float dpiScale = DpiHelper.GetDPIScaleForWindow((int)hwnd);

                    // Maintain 600 pixels width and full monitor height
                    this.SetWindowSize(600 * dpiScale, monitorRect.Height / dpiScale);

                    // Keep window at the left edge of the monitor
                    _appWindow.Move(new PointInt32((int)monitorRect.X, (int)monitorRect.Y));
                };
            }

            // Populate the window selector with the current application IDs if it is empty.
            // TO DO: Check if Settings button is considered an item too.
            if (WindowSelector.MenuItems.Count == 0)
            {
                foreach (var item in _currentApplicationIds)
                {
                    if (item == ManifestInterpreter.GetIndexYamlFile().DefaultShellName)
                    {
                        WindowSelector.MenuItems.Add(new NavigationViewItem { Name = item, Content = "Windows", Icon = new FontIcon() { Glyph = "\xE770" } });
                    }
                    else
                    {
                        try
                        {
                            WindowSelector.MenuItems.Add(new NavigationViewItem { Name = item, Content = ManifestInterpreter.GetShortcutsOfApplication(item).Name, Icon = new FontIcon { Glyph = "\uEB91" } });
                        }
                        catch (IOException)
                        {
                        }
                    }
                }

                WindowSelector.SelectedItem = WindowSelector.MenuItems[0];
            }
        }

        public void CloseButton_Clicked(object sender, RoutedEventArgs e)
        {
            ShortcutView.AnimationCancellationTokenSource.Cancel();
            Close();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ShortcutPageParameters.SearchFilter.OnFilterChanged(SearchBox.Text);
        }

        private void SearchBox_KeyboardAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            SearchBox.Focus(FocusState.Programmatic);
        }

        private void WindowSelector_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                SettingsDeepLink.OpenSettings(SettingsDeepLink.SettingsWindow.ShortcutGuide, true);
            }
            else
            {
                if (args.SelectedItem is NavigationViewItem selectedItem)
                {
                    string newPageName = selectedItem.Name;
                    ShortcutPageParameters.CurrentPageName = newPageName;
                    ContentFrame.Loaded += (_, _) => ShortcutPageParameters.FrameHeight.OnFrameHeightChanged(ContentFrame.ActualHeight);
                    ContentFrame.Navigate(typeof(ShortcutView));

                    // I don't know why this has to be called again, but it does.
                    ShortcutPageParameters.FrameHeight.OnFrameHeightChanged(ContentFrame.ActualHeight);
                }
            }
        }
    }
}
