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
        private readonly Task<Dictionary<string, string?>> _getAppIdsTask;
        private Dictionary<string, string?> _currentApplicationIds = [];
        private ShortcutFile? _shortcutFile;
        private string _selectedAppName = string.Empty;
        private bool _navItemsInitialized;

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

            // Same background work the original MainWindow ran in its
            // constructor: wait for the index-generation thread to finish
            // and then enumerate the apps to populate the nav list.
            _getAppIdsTask = Task.Run(() =>
            {
                Program.CopyAndIndexGenerationThread.Join();
                _currentApplicationIds = ManifestInterpreter.GetAllCurrentApplicationIds(Program.ForegroundWindowHandle);
                return _currentApplicationIds;
            });

            this.TitleTextBlock.Text = ResourceLoaderInstance.ResourceLoader.GetString("Title");

            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
        }

        internal string SelectedAppName => _selectedAppName;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _ = InitializeNavItemsAsync();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _getAppIdsTask.Dispose();
        }

        /// <summary>
        /// Awaits the background app-id enumeration and populates the nav
        /// list. Safe to call repeatedly: the work only runs once.
        /// </summary>
        private async Task InitializeNavItemsAsync()
        {
            if (_navItemsInitialized)
            {
                return;
            }

            try
            {
                _currentApplicationIds = await _getAppIdsTask.ConfigureAwait(true);
                this.SetNavItems();
                _navItemsInitialized = true;
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

        private void SetNavItems()
        {
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

        private void WindowSelector_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is not NavigationViewItem selectedItem)
            {
                return;
            }

            this._selectedAppName = selectedItem.Name;
            App.CurrentAppName = this._selectedAppName;
            this._shortcutFile = ManifestInterpreter.GetShortcutsOfApplication(this._selectedAppName);

            bool exposesTaskbarSection = false;

            if (this._shortcutFile is ShortcutFile file)
            {
                exposesTaskbarSection = file.Shortcuts is not null &&
                    file.Shortcuts.Any(c => c.SectionName?.StartsWith("<TASKBAR1-9>", StringComparison.Ordinal) == true);

                this.ContentFrame.Navigate(
                    typeof(ShortcutsPage),
                    new ShortcutPageNavParam { ShortcutFile = file, AppName = this._selectedAppName });
            }

            // The overlay decides whether to show/hide the taskbar pseudo-window
            // based on this event — the pane no longer manages a second window
            // directly.
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
