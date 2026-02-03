// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Common.UI;
using ManagedCommon;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
        private readonly string[] _currentApplicationIds;
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
                foreach (var item in this._currentApplicationIds)
                {
                    if (item == ManifestInterpreter.GetIndexYamlFile().DefaultShellName)
                    {
                        this.WindowSelector.MenuItems.Add(new NavigationViewItem { Name = item, Content = "Windows", Icon = new FontIcon() { Glyph = "\xE770" } });
                    }
                    else
                    {
                        try
                        {
                            this.WindowSelector.MenuItems.Add(new NavigationViewItem { Name = item, Content = ManifestInterpreter.GetShortcutsOfApplication(item).Name, Icon = new FontIcon { Glyph = "\uEB91" } });
                        }
                        catch (IOException)
                        {
                        }
                    }
                }

                this.WindowSelector.SelectedItem = this.WindowSelector.MenuItems[0];
            }
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
            if (App.TaskBarWindow.AppWindow.IsVisible && App.TaskBarWindow.AppWindow.Position.X < AppWindow.Position.X + Width)
            {
                MaxHeight = (monitorRect.Height / dpi) - App.TaskBarWindow.AppWindow.Size.Height;
                MinHeight = MaxHeight;
                Height = MaxHeight;
            }
            else
            {
                MaxHeight = monitorRect.Height / DpiHelper.GetDPIScaleForWindow(hwnd.ToInt32());
                MinHeight = MaxHeight;
                Height = MaxHeight;
            }

            this.MoveAndResize((int)monitorRect.X, (int)monitorRect.Y, Width, Height);
        }

        /*private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            // TO DO: should the results of this be shown on a separate results page? Or as part of the suggested items of the search box?
            // The current UX is a bit weird as search is about the content that is selected on the page, vs. global search (which a search box in the title bar communicates).
            // Also, the results indicate that they can be clicked - but they cannot, so this needs more UX thinking on having the right model.
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && !string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                ObservableCollection<ShortcutEntry> searchResults = new ObservableCollection<ShortcutEntry>();

                if (_shortcutFile is ShortcutFile file)
                {
                    foreach (var shortcut in file.Shortcuts.SelectMany(list => list.Properties.Where(s => s.Name.Contains(SearchBox.Text, StringComparison.InvariantCultureIgnoreCase))))
                    {
                        searchResults.Add(shortcut);
                    }

                    SearchBox.ItemsSource = searchResults;
                }
            }
        }

        private void SearchBox_KeyboardAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            SearchBox.Focus(FocusState.Programmatic);
        }*/

        private void WindowSelector_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem selectedItem)
            {
                this._selectedAppName = selectedItem.Name;
                this._shortcutFile = ManifestInterpreter.GetShortcutsOfApplication(this._selectedAppName);
                this.PopulateCategorySelector();
            }
        }

        private void PopulateCategorySelector()
        {
            this.SubNav.MenuItems.Clear();
            this.SubNav.MenuItems.Add(new NavigationViewItem()
            {
                Content = ResourceLoaderInstance.ResourceLoader.GetString("Overview"),
                Tag = -1,
            });

            int i = 0;

            if (this._shortcutFile is ShortcutFile file)
            {
                foreach (var category in file.Shortcuts)
                {
                    switch (category.SectionName)
                    {
                        case { } name when name.StartsWith("<TASKBAR1-9>", StringComparison.Ordinal):
                            break;
                        case { } name when name.StartsWith('<') && name.EndsWith('>'):
                            break;
                        default:
                            this.SubNav.MenuItems.Add(new NavigationViewItem() { Content = category.SectionName, Tag = i });
                            break;
                    }

                    i++;
                }

                if (this.SubNav.MenuItems.Count > 0)
                {
                    this.SubNav.SelectedItem = this.SubNav.MenuItems[0];
                }
            }
        }

        /// <summary>
        /// Tracks whether the taskbar window was activated. So that the main window does not close.
        /// </summary>
        private bool _taskBarWindowActivated;

        private void SubNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem selectedItem && selectedItem.Tag is int param && this._shortcutFile is ShortcutFile file)
            {
                Type selectedPage = typeof(ShortcutsPage);
                App.TaskBarWindow.Hide();
                if (param == -1)
                {
                    selectedPage = typeof(OverviewPage);

                    // We only show the taskbar button window when the overview page of Windows is selected.
                    if (this._shortcutFile is not null && this._shortcutFile.Value.Shortcuts.Any(c => c.SectionName.Contains("<TASKBAR1-9>")))
                    {
                        this._taskBarWindowActivated = true;
                        App.TaskBarWindow.Activate();
                    }
                }

                // Set window position so that the taskbar window does not potentially clip into the main window
                this.SetWindowPosition();
                this.ContentFrame.Navigate(selectedPage, new ShortcutPageNavParam() { ShortcutFile = file, PageIndex = param, AppName = this._selectedAppName });
            }
        }

        private void Settings_Tapped(object sender, TappedRoutedEventArgs e)
        {
            SettingsDeepLink.OpenSettings(SettingsDeepLink.SettingsWindow.ShortcutGuide);
        }
    }
}
