// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Common.UI;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using ShortcutGuide.Helpers;
using ShortcutGuide.Models;
using ShortcutGuide.Pages;
using ShortcutGuide.ShortcutGuideXAML.Controls;

namespace ShortcutGuide.Controls
{
    /// <summary>
    /// The big shortcut-list pseudo-window that used to be <c>MainWindow</c>.
    /// Now a regular <see cref="UserControl"/> hosted inside
    /// <see cref="OverlayWindow"/>, so it can be sized / positioned / animated
    /// via XAML layout instead of by driving a real <c>WindowEx</c>.
    /// </summary>
    public sealed partial class MainPaneControl : UserControl
    {
        private Task<Dictionary<string, string?>>? _getAppIdsTask;
        private Dictionary<string, string?> _currentApplicationIds = [];
        private ShortcutFile? _shortcutFile;
        private string _selectedAppName = string.Empty;

        /// <summary>
        /// Raised whenever the user selects a different app in the nav list.
        /// The boolean payload indicates whether the newly-selected app
        /// exposes a <c>&lt;TASKBAR1-9&gt;</c> section, i.e. whether the
        /// overlay should reveal the taskbar number pseudo-window.
        /// </summary>
        public event EventHandler<bool>? SelectedAppTaskbarVisibilityChanged;

        /// <summary>
        /// Raised when the user clicks the close button. The overlay window
        /// handles this the same way as pressing Escape.
        /// </summary>
        public event EventHandler? CloseRequested;

        public MainPaneControl()
        {
            this.InitializeComponent();
            Program.CopyAndIndexGenerationThread.Join();
            this.TitleTextBlock.Text = ResourceLoaderInstance.ResourceLoader.GetString("Title");

            this.Unloaded += OnUnloaded;
        }

        public async Task Open()
        {
            // Same background work the original MainWindow ran in its
            // constructor: wait for the index-generation thread to finish
            // and then enumerate the apps to populate the nav list.
            _getAppIdsTask = Task.Run(async () =>
            {
                _currentApplicationIds = ManifestInterpreter.GetAllCurrentApplicationIds(Program.ForegroundWindowHandle);
                return _currentApplicationIds;
            });

            await InitializeNavItemsAsync();
        }

        public void Hide()
        {
            if (this.ContentFrame.Content is ShortcutsPage currentPage)
            {
                currentPage.Rows.Clear();
            }

            this.ContentFrame.Navigate(typeof(BlankPage));

            if (this.ContentFrame.BackStack != null)
            {
                this.ContentFrame.BackStack.Clear();
            }

            this.ContentFrame.Content = null;

            foreach (var item in this.WindowSelector.MenuItems.OfType<NavigationViewItem>())
            {
                if (item.Icon is ImageIcon imageIcon)
                {
                    imageIcon.Source = null;
                }

                if (item.Icon is PathIcon pathIcon)
                {
                    pathIcon.Data = null;
                }

                item.Icon = null;
                item.Content = null;
            }

            this.WindowSelector.MenuItems.Clear();

            _shortcutFile = null;
            _currentApplicationIds.Clear();

            _getAppIdsTask?.Dispose();
            _getAppIdsTask = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        internal string SelectedAppName => _selectedAppName;

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _getAppIdsTask?.Dispose();
        }

        /// <summary>
        /// Awaits the background app-id enumeration and populates the nav
        /// list. Safe to call repeatedly: the work only runs once.
        /// </summary>
        private async Task InitializeNavItemsAsync()
        {
            if (_getAppIdsTask == null)
            {
                return;
            }

            try
            {
                _currentApplicationIds = await _getAppIdsTask.ConfigureAwait(true);
                this.SetNavItems();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize navigation items.", ex);

                // Surface the failure so the overlay can close itself,
                // mirroring the original MainWindow.InitializeNavItemsAsync
                // behavior.
                this.DispatcherQueue.TryEnqueue(() => InitializationFailed?.Invoke(this, EventArgs.Empty));
            }
        }

        /// <summary>
        /// Raised when the background app-id enumeration fails. The overlay
        /// listens to this so it can close itself (same behavior the
        /// original <c>MainWindow.InitializeNavItemsAsync</c> hand-coded).
        /// </summary>
        public event EventHandler? InitializationFailed;

        private IconElement BuildNavIcon(string? executablePath)
        {
            // FIX: Use placeholder initially to reduce upfront memory
            // Icons can be loaded on SelectionChanged if needed
            return new FontIcon { Glyph = "\uEB91" };
        }

        private void SetNavItems()
        {
            this.WindowSelector.MenuItems.Clear();
            if (this.WindowSelector.MenuItems.Count != 0)
            {
                return;
            }

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

        private void WindowSelector_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is not NavigationViewItem selectedItem)
            {
                return;
            }

            if (selectedItem.Icon is FontIcon && _currentApplicationIds.TryGetValue(selectedItem.Name, out string? exePath))
            {
                BitmapImage? bitmap = IconHelper.TryGetExecutableIcon(exePath);
                if (bitmap is not null)
                {
                    selectedItem.Icon = new ImageIcon { Source = bitmap };
                }
            }

            this._selectedAppName = selectedItem.Name;
            App.CurrentAppName = this._selectedAppName;
            this._shortcutFile = ManifestInterpreter.GetShortcutsOfApplication(this._selectedAppName);

            bool exposesTaskbarSection = false;

            if (this._shortcutFile is ShortcutFile file)
            {
                exposesTaskbarSection = file.Shortcuts is not null &&
                    file.Shortcuts.Any(c => c.SectionName?.StartsWith("<TASKBAR1-9>", StringComparison.Ordinal) == true);

                if (this.ContentFrame.Content is ShortcutsPage currentPage)
                {
                    currentPage.ClearData();
                }

                this.ContentFrame.Navigate(
                    typeof(ShortcutsPage),
                    new ShortcutPageNavParam { ShortcutFile = file, AppName = this._selectedAppName });
            }

            SelectedAppTaskbarVisibilityChanged?.Invoke(this, exposesTaskbarSection);
        }

        private void Settings_Tapped(object sender, TappedRoutedEventArgs e)
        {
            SettingsDeepLink.OpenSettings(SettingsDeepLink.SettingsWindow.ShortcutGuide);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
