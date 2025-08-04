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
    public sealed partial class MainWindow
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
            this.SetIsResizable(false);
            this.SetIsMinimizable(false);
            this.SetIsMaximizable(false);
            IsTitleBarVisible = false;

            // Remove the caption style from the window style. Windows App SDK 1.6 added it, which made the title bar and borders appear. This code removes it.
            var windowStyle = GetWindowLongW(hwnd, GWL_STYLE);
            windowStyle &= ~WS_CAPTION;
            _ = SetWindowLongW(hwnd, GWL_STYLE, windowStyle);

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

                GetCursorPos(out POINT lpPoint);
                _appWindow.Move(new POINT { Y = lpPoint.Y - ((int)Height / 2), X = lpPoint.X - ((int)Width / 2) });

                float dpiScale = DpiHelper.GetDPIScaleForWindow((int)hwnd);

                Rect monitorRect = DisplayHelper.GetWorkAreaForDisplayWithWindow(hwnd);
                this.SetWindowSize(monitorRect.Width / dpiScale, monitorRect.Height / dpiScale / 2);

                // Move top of the window to the center of the monitor
                _appWindow.Move(new PointInt32((int)monitorRect.X, (int)(monitorRect.Y + (int)(monitorRect.Height / 2))));
                _setPosition = true;
                AppWindow.Changed += (_, a) =>
                {
                    if (!a.DidPresenterChange)
                    {
                        return;
                    }

                    Rect monitorRect = DisplayHelper.GetWorkAreaForDisplayWithWindow(hwnd);
                    float dpiScale = DpiHelper.GetDPIScaleForWindow((int)hwnd);
                    this.SetWindowSize(monitorRect.Width / dpiScale, monitorRect.Height / dpiScale / 2);
                    _appWindow.Move(new PointInt32((int)monitorRect.X, (int)(monitorRect.Y + (int)(monitorRect.Height / 2))));
                };
            }

            // Populate the window selector with the current application IDs if it is empty.
            if (WindowSelector.Items.Count == 0)
            {
                foreach (var item in _currentApplicationIds)
                {
                    if (item == ManifestInterpreter.GetIndexYamlFile().DefaultShellName)
                    {
                        WindowSelector.Items.Add(new SelectorBarItem { Name = item, Text = "Windows", Icon = new FontIcon() { Glyph = "\xE770" } });
                    }
                    else
                    {
                        try
                        {
                            WindowSelector.Items.Add(new SelectorBarItem { Name = item, Text = ManifestInterpreter.GetShortcutsOfApplication(item).Name, Icon = new FontIcon { Glyph = "\uEB91" } });
                        }
                        catch (IOException)
                        {
                        }
                    }
                }

                if (_firstRun)
                {
                    CreateAndOpenWelcomePage();
                }

                WindowSelector.SelectedItem = WindowSelector.Items[0];
            }
        }

        public void WindowSelectionChanged(object sender, SelectorBarSelectionChangedEventArgs e)
        {
            string newPageName = ((SelectorBar)sender).SelectedItem.Name;

            if (newPageName == "<WELCOME>")
            {
                ContentFrame.Navigate(typeof(OOBEView));
                return;
            }

            ShortcutPageParameters.CurrentPageName = newPageName;

            ContentFrame.Loaded += (_, _) => ShortcutPageParameters.FrameHeight.OnFrameHeightChanged(ContentFrame.ActualHeight);

            ContentFrame.Navigate(typeof(ShortcutView));

            // I don't know why this has to be called again, but it does.
            ShortcutPageParameters.FrameHeight.OnFrameHeightChanged(ContentFrame.ActualHeight);
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

        private void SettingsButton_Clicked(object sender, RoutedEventArgs e)
        {
            SettingsDeepLink.OpenSettings(SettingsDeepLink.SettingsWindow.ShortcutGuide, true);
        }

        private void InformationButton_Click(object sender, RoutedEventArgs e)
        {
            InformationTip.IsOpen = !InformationTip.IsOpen;
        }

        private void HyperlinkButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowSelector.Items[0].Name == "<WELCOME>")
            {
                WindowSelector.SelectedItem = WindowSelector.Items[0];
            }
            else
            {
                CreateAndOpenWelcomePage();
            }
        }

        /// <summary>
        /// Adds the welcome page to the window selector and opens it.
        /// </summary>
        private void CreateAndOpenWelcomePage()
        {
            WindowSelector.Items.Insert(0, new SelectorBarItem { Name = "<WELCOME>", Text = ResourceLoaderInstance.ResourceLoader.GetString("Welcome"), Icon = new FontIcon { Glyph = "\uE789" } });
            WindowSelector.SelectedItem = WindowSelector.Items[0];
        }
    }
}
