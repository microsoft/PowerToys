// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class ContextMenuManagerPage : NavigablePage, IRefreshablePage
    {
        private ContextMenuManagerViewModel ViewModel { get; }

        public ContextMenuManagerPage()
        {
            InitializeComponent();
            var settingsUtils = SettingsUtils.Default;
            ViewModel = new ContextMenuManagerViewModel(settingsUtils, SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage, App.IsElevated);
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }

        // Intercepts the details toggle instead of a plain two-way binding, so HKLM (all-users) writes
        // and likely-Windows-owned entries can be confirmed before anything is written to the registry.
        private async void DetailsToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            var toggle = (ToggleSwitch)sender;

            // Selecting another item re-binds IsOn to that entry's state, which lands here as a no-op below.
            if (!toggle.IsLoaded || ViewModel.SelectedEntry is not ContextMenuEntry entry)
            {
                return;
            }

            bool requestedState = toggle.IsOn;
            if (requestedState == entry.IsEnabled)
            {
                return;
            }

            if (!await ConfirmIfNeededAsync(entry))
            {
                toggle.IsOn = entry.IsEnabled;
                return;
            }

            if (!ViewModel.ToggleEntry(entry, requestedState))
            {
                toggle.IsOn = entry.IsEnabled;
            }
        }

        // Selects the row for the details pane, and opens its submenu the way Explorer would: to the right of the row.
        private void PreviewItem_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            if (button.DataContext is not ContextMenuPreviewItem item)
            {
                return;
            }

            if (item.IsShowMoreOptions)
            {
                ViewModel.ShowClassicLayer();
                return;
            }

            ViewModel.SelectedPreviewItem = item;
            if (!item.HasChildren)
            {
                return;
            }

            var flyout = new MenuFlyout { Placement = FlyoutPlacementMode.RightEdgeAlignedTop };
            AddPreviewMenuItems(flyout.Items, item.Children);
            flyout.ShowAt(button);
        }

        private void AddPreviewMenuItems(IList<MenuFlyoutItemBase> target, IEnumerable<ContextMenuPreviewItem> items)
        {
            foreach (var child in items)
            {
                if (child.IsSeparator)
                {
                    target.Add(new MenuFlyoutSeparator());
                    continue;
                }

                IconElement icon = child.Icon != null ? new ImageIcon { Source = child.Icon } : null;
                if (child.HasChildren)
                {
                    var sub = new MenuFlyoutSubItem { Text = child.Text, Icon = icon };
                    AddPreviewMenuItems(sub.Items, child.Children);
                    target.Add(sub);
                }
                else
                {
                    var menuItem = new MenuFlyoutItem { Text = child.Text, Icon = icon, KeyboardAcceleratorTextOverride = child.SideText ?? string.Empty, IsEnabled = !child.IsDisabled };
                    menuItem.Click += (_, _) => ViewModel.SelectedPreviewItem = child;
                    target.Add(menuItem);
                }
            }
        }

        private async Task<bool> ConfirmIfNeededAsync(ContextMenuEntry entry)
        {
            // Blast-radius confirmation: an all-users write affects every account on the machine.
            if (entry.Scope == ContextMenuEntryScope.AllUsers)
            {
                if (!await ShowConfirmDialogAsync("ContextMenuManager_ConfirmAllUsers"))
                {
                    return false;
                }
            }

            // Extra confirmation (not a hard block) for entries whose handler DLL resolves under
            // System32/SysWOW64 - these are usually built-in Windows or security-software handlers.
            if (entry.IsLikelyWindowsOwned)
            {
                if (!await ShowConfirmDialogAsync("ContextMenuManager_ConfirmWindowsOwned"))
                {
                    return false;
                }
            }

            return true;
        }

        private async Task<bool> ShowConfirmDialogAsync(string resourceUidPrefix)
        {
            var dialog = new ContentDialog
            {
                Title = ResourceLoaderInstance.ResourceLoader.GetString(resourceUidPrefix + "_Title"),
                Content = ResourceLoaderInstance.ResourceLoader.GetString(resourceUidPrefix + "_Content"),
                PrimaryButtonText = ResourceLoaderInstance.ResourceLoader.GetString(resourceUidPrefix + "_PrimaryButton"),
                CloseButtonText = ResourceLoaderInstance.ResourceLoader.GetString(resourceUidPrefix + "_CloseButton"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot,
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }
    }
}
