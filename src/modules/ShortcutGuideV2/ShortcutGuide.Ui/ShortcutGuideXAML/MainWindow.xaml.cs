// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ShortcutGuide.Models;
using ShortcutGuide.Properties;
using Windows.Foundation;
using Windows.Graphics;
using WinUIEx;
using static NativeMethods;

namespace ShortcutGuide
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : WindowEx
    {
        private AppWindow _appWindow;

        private string[] _currentApplicationIds;

        public MainWindow()
        {
            _currentApplicationIds = ManifestInterpreter.GetAllCurrentApplicationIds();

            InitializeComponent();

            Title = Resource.ResourceManager.GetString("Title", CultureInfo.InvariantCulture)!;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
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

            // Remove the caption style from the window style. Windows App SDK 1.6 added it, which made the title bar and borders appear for Measure Tool. This code removes it.
            var windowStyle = GetWindowLongW(hwnd, GWL_STYLE);
            windowStyle &= ~WS_CAPTION;
            _ = SetWindowLongW(hwnd, GWL_STYLE, windowStyle);
#if !DEBUG
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
#endif

            Activated += OnLauched;

            SettingsUtils settingsUtils = new();

            if (settingsUtils.SettingsExists(ShortcutGuideSettings.ModuleName, "Pinned.json"))
            {
                string pinnedPath = settingsUtils.GetSettingsFilePath(ShortcutGuideSettings.ModuleName, "Pinned.json");
                ShortcutPageParameters.PinnedShortcuts = JsonSerializer.Deserialize<Dictionary<string, List<ShortcutEntry>>>(File.ReadAllText(pinnedPath))!;
            }
        }

        private void OnLauched(object sender, WindowActivatedEventArgs e)
        {
            if (!_setPosition)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);

                _appWindow = AppWindow.GetFromWindowId(windowId);

                GetCursorPos(out POINT lpPoint);
                _appWindow.Move(lpPoint with { Y = lpPoint.Y - ((int)Height / 2), X = lpPoint.X - ((int)Width / 2) });

                float dpiScale = DpiHelper.GetDPIScaleForWindow((int)hwnd);

                Rect monitorRect = DisplayHelper.GetWorkAreaForDisplayWithWindow(hwnd);
                this.SetWindowSize(monitorRect.Width / dpiScale, monitorRect.Height / dpiScale / 2);

                // Move top of the window to the center of the monitor
                _appWindow.Move(new PointInt32((int)monitorRect.X, (int)(monitorRect.Y + (int)(monitorRect.Height / 2))));
                _setPosition = true;
            }

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

                WindowSelector.SelectedItem = WindowSelector.Items[0];
            }
        }

        public void WindowSelectionChanged(object sender, SelectorBarSelectionChangedEventArgs e)
        {
            ShortcutPageParameters.CurrentPageName = ((SelectorBar)sender).SelectedItem.Name;

            ContentFrame.Loaded += (_, _) => ShortcutPageParameters.FrameHeight.OnFrameHeightChanged(ContentFrame.ActualHeight);

            ContentFrame.Navigate(typeof(ShortcutView));

            // I don't know why this has to be called again, but it does.
            ShortcutPageParameters.FrameHeight.OnFrameHeightChanged(ContentFrame.ActualHeight);
        }

        private bool _setPosition;

        public void CloseButton_Clicked(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ShortcutPageParameters.SearchFilter.OnFilterChanged(SearchBox.Text);
        }

        private void SearchBoy_KeyboardAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            SearchBox.Focus(FocusState.Programmatic);
        }
    }
}
