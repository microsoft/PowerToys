// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Text.Json;
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
        /*private readonly bool _isInWindowsKeyMode;*/

        public static nint WindowHwnd { get; set; }

        private AppWindow _appWindow;

        public MainWindow()
        {
            _currentApplicationIds = ManifestInterpreter.GetAllCurrentApplicationIds();

            InitializeComponent();

            // Todo: Reimplement holding the Windows key down to show the guide.
            /*
            _isInWindowsKeyMode = (GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0;

            if (_isInWindowsKeyMode)
            {
                Current.CoreWindow.KeyUp += (_, e) =>
                {
                    if (e.VirtualKey is (VirtualKey)0x5B or (VirtualKey)0x5C)
                    {
                        Close();
                    }
                };
            }*/

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

            // Remove the caption style from the window style. Windows App SDK 1.6 added it, which made the title bar and borders appear for Measure Tool. This code removes it.
            var windowStyle = GetWindowLongW(hwnd, GWL_STYLE);
            windowStyle &= ~WS_CAPTION;
            _ = SetWindowLongW(hwnd, GWL_STYLE, windowStyle);
#if !DEBUG
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
#endif

            Activated += Window_Activated;

            SettingsUtils settingsUtils = new();

            if (settingsUtils.SettingsExists(ShortcutGuideSettings.ModuleName, "Pinned.json"))
            {
                string pinnedPath = settingsUtils.GetSettingsFilePath(ShortcutGuideSettings.ModuleName, "Pinned.json");
                ShortcutPageParameters.PinnedShortcuts = JsonSerializer.Deserialize<Dictionary<string, List<ShortcutEntry>>>(File.ReadAllText(pinnedPath))!;
            }
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState == WindowActivationState.Deactivated)
            {
#if !DEBUG
                Close();
#endif
            }

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
            ShortcutView.AnimationCancellationTokenSource.Cancel();
            Close();
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
