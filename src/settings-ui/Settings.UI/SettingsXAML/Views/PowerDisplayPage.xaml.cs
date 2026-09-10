// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Controls;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PowerDisplay.Models;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Core;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class PowerDisplayPage : NavigablePage, IRefreshablePage
    {
        private int? _draggedProfileId;
        private int[] _profileOrderBeforeDrag;
        private int _profileFocusRequestId;
        private Action _cancelPendingProfileFocus;

        private PowerDisplayViewModel ViewModel { get; set; }

        public PowerDisplayPage()
        {
            var settingsUtils = SettingsUtils.Default;
            ViewModel = new PowerDisplayViewModel(
                settingsUtils,
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                SettingsRepository<PowerDisplaySettings>.GetInstance(settingsUtils),
                ShellPage.SendDefaultIPCMessage);
            ViewModel.ConfirmDangerousFeatureAsync = ShowDangerousFeatureDialogAsync;
            DataContext = ViewModel;
            InitializeComponent();
            Loaded += PowerDisplayPage_Loaded;
            SizeChanged += PowerDisplayPage_SizeChanged;
            Unloaded += (_, _) => CancelProfileFocusRestoration();
        }

        private async void PowerDisplayPage_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.OnPageLoaded();
            await ViewModel.InitializeProfilesAsync();
        }

        private async Task<bool> ShowDangerousFeatureDialogAsync(PowerDisplayWarningKind kind)
        {
            var dialog = new PowerDisplayWarningDialog(kind) { XamlRoot = XamlRoot };
            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }

        private void CopyVcpCodes_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is MonitorInfo monitor)
            {
                var vcpText = monitor.GetVcpCodesAsText();
                var dataPackage = new DataPackage();
                dataPackage.SetText(vcpText);
                Clipboard.SetContent(dataPackage);
            }
        }

        private void CopyMonitorDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is MonitorInfo monitor)
            {
                var diagnosticsText = monitor.GetDiagnosticsAsText();

                var dataPackage = new DataPackage();
                dataPackage.SetText(diagnosticsText);
                Clipboard.SetContent(dataPackage);
            }
        }

        // Profile button event handlers
        private void PowerDisplayPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // ListView needs a bounded viewport for native edge scrolling while reordering.
            ProfilesList.MaxHeight = Math.Max(120, e.NewSize.Height / 2);
        }

        private async void ProfilesList_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Handled ||
                e.OriginalKey is not (VirtualKey.Up or VirtualKey.Down or VirtualKey.Left or VirtualKey.Right) ||
                !IsKeyDown(VirtualKey.Menu) || !IsKeyDown(VirtualKey.Shift) || IsKeyDown(VirtualKey.Control))
            {
                return;
            }

            if (e.OriginalSource is not DependencyObject source ||
                source.FindAscendantOrSelf<ListViewItem>() is not ListViewItem container ||
                ItemsControl.ItemsControlFromItemContainer(container) != ProfilesList ||
                ProfilesList.ItemFromContainer(container) is not PowerDisplayProfile profile)
            {
                return;
            }

            // Native keyboard reordering changes ItemsSource without raising DragItemsCompleted.
            // Handle the shared key event before awaiting so it cannot also move the item.
            e.Handled = true;
            if (_draggedProfileId.HasValue)
            {
                return;
            }

            await MoveProfileAndRestoreFocusAsync(profile, e.OriginalKey is VirtualKey.Up or VirtualKey.Left);
        }

        private static bool IsKeyDown(VirtualKey key)
            => InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);

        private void ProfilesList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (!args.InRecycleQueue && args.Item is PowerDisplayProfile profile)
            {
                AutomationProperties.SetName(args.ItemContainer, profile.DisplayName);
            }
            else
            {
                args.ItemContainer.ClearValue(AutomationProperties.NameProperty);
            }
        }

        private void ProfilesList_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            CancelProfileFocusRestoration();
            _draggedProfileId = null;
            _profileOrderBeforeDrag = null;

            if (!ViewModel.CanDragProfiles || e.Items.Count != 1 || e.Items[0] is not PowerDisplayProfile profile || !ViewModel.Profiles.Contains(profile))
            {
                e.Cancel = true;
                return;
            }

            _draggedProfileId = profile.Id;
            _profileOrderBeforeDrag = ViewModel.Profiles.Select(item => item.Id).ToArray();
            e.Data.RequestedOperation = DataPackageOperation.Move;
        }

        private async void ProfilesList_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            var profileId = _draggedProfileId;
            var previousOrder = _profileOrderBeforeDrag;
            _draggedProfileId = null;
            _profileOrderBeforeDrag = null;

            if (sender != ProfilesList || args.DropResult != DataPackageOperation.Move || profileId is not int draggedId || previousOrder == null ||
                args.Items.Count != 1 || args.Items[0] is not PowerDisplayProfile profile || profile.Id != draggedId)
            {
                return;
            }

            // ListView has already reordered the collection. Save only a completed
            // reorder of this list, leaving canceled drags and external drops alone.
            var currentOrder = ViewModel.Profiles.Select(item => item.Id).ToArray();
            if (previousOrder.SequenceEqual(currentOrder) || !previousOrder.OrderBy(id => id).SequenceEqual(currentOrder.OrderBy(id => id)))
            {
                return;
            }

            var newIndex = Array.IndexOf(currentOrder, draggedId);
            if (newIndex < 0)
            {
                return;
            }

            int? beforeProfileId = newIndex + 1 < currentOrder.Length ? currentOrder[newIndex + 1] : null;
            CancelProfileFocusRestoration();
            var focusRequestId = _profileFocusRequestId;
            await ViewModel.ReorderProfileAsync(draggedId, beforeProfileId);
            RestoreProfileFocus(draggedId, focusRequestId);
        }

        private void ProfileMenuFlyout_Opening(object sender, object e)
        {
            if (sender is not MenuFlyout flyout)
            {
                return;
            }

            foreach (var item in flyout.Items.OfType<MenuFlyoutItem>())
            {
                if (item.Tag is not PowerDisplayProfile profile)
                {
                    continue;
                }

                if (item.Name == "MoveProfileUpMenuItem")
                {
                    item.IsEnabled = ViewModel.CanMoveProfileUp(profile);
                }
                else if (item.Name == "MoveProfileDownMenuItem")
                {
                    item.IsEnabled = ViewModel.CanMoveProfileDown(profile);
                }
            }
        }

        private async void MoveProfileUp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is PowerDisplayProfile profile)
            {
                await MoveProfileAndRestoreFocusAsync(profile, moveUp: true);
            }
        }

        private async void MoveProfileDown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is PowerDisplayProfile profile)
            {
                await MoveProfileAndRestoreFocusAsync(profile, moveUp: false);
            }
        }

        private async Task MoveProfileAndRestoreFocusAsync(PowerDisplayProfile profile, bool moveUp)
        {
            if (moveUp ? !ViewModel.CanMoveProfileUp(profile) : !ViewModel.CanMoveProfileDown(profile))
            {
                return;
            }

            CancelProfileFocusRestoration();
            var focusRequestId = _profileFocusRequestId;
            if (moveUp)
            {
                await ViewModel.MoveProfileUpAsync(profile);
            }
            else
            {
                await ViewModel.MoveProfileDownAsync(profile);
            }

            RestoreProfileFocus(profile.Id, focusRequestId);
        }

        private void CancelProfileFocusRestoration()
        {
            _profileFocusRequestId++;
            _cancelPendingProfileFocus?.Invoke();
            _cancelPendingProfileFocus = null;
        }

        private void RestoreProfileFocus(int profileId, int requestId)
        {
            // Let the menu close and the collection refresh settle before requesting layout.
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                if (!IsLoaded || requestId != _profileFocusRequestId || !ViewModel.CanUseProfiles)
                {
                    return;
                }

                var profile = ViewModel.Profiles.FirstOrDefault(item => item.Id == profileId);
                if (profile == null)
                {
                    return;
                }

                // Clear/Add invalidates the virtualizing panel's item-to-container mapping.
                // Update it before scrolling; the target container may still arrive in a later layout.
                ProfilesList.UpdateLayout();
                var previousFocus = FocusManager.GetFocusedElement(XamlRoot);
                var timeout = DispatcherQueue.CreateTimer();
                timeout.Interval = TimeSpan.FromSeconds(1);
                timeout.IsRepeating = false;
                var completed = false;

                void Complete()
                {
                    if (completed)
                    {
                        return;
                    }

                    completed = true;
                    ProfilesList.LayoutUpdated -= OnLayoutUpdated;
                    timeout.Tick -= OnTimeout;
                    timeout.Stop();
                    if (requestId == _profileFocusRequestId)
                    {
                        _cancelPendingProfileFocus = null;
                    }
                }

                void TryRestoreFocus()
                {
                    if (completed)
                    {
                        return;
                    }

                    if (!IsLoaded || requestId != _profileFocusRequestId || !ViewModel.CanUseProfiles)
                    {
                        Complete();
                        return;
                    }

                    var container = ProfilesList.ContainerFromItem(profile) as ListViewItem;
                    var moreButton = container?.FindDescendants().OfType<Button>().FirstOrDefault(button => button.Name == "ProfileMoreButton");
                    var currentFocus = FocusManager.GetFocusedElement(XamlRoot);
                    if (currentFocus != null && !ReferenceEquals(currentFocus, previousFocus) && !ReferenceEquals(currentFocus, moreButton))
                    {
                        // A newer user interaction must not lose its focus to this pending request.
                        Complete();
                        return;
                    }

                    if (moreButton is { IsLoaded: true, IsEnabled: true, ActualWidth: > 0, ActualHeight: > 0 })
                    {
                        moreButton.StartBringIntoView();
                        if (moreButton.Focus(FocusState.Programmatic))
                        {
                            Complete();
                        }
                    }
                }

                void OnLayoutUpdated(object sender, object args) => TryRestoreFocus();
                void OnTimeout(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args) => Complete();

                _cancelPendingProfileFocus = Complete;
                ProfilesList.LayoutUpdated += OnLayoutUpdated;
                timeout.Tick += OnTimeout;
                timeout.Start();
                ProfilesList.ScrollIntoView(profile);
                TryRestoreFocus();
            });
        }

        private void ProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PowerDisplayProfile profile)
            {
                ViewModel.ApplyProfile(profile);
            }
        }

        private async void AddProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Monitors == null || ViewModel.Monitors.Count == 0)
            {
                return;
            }

            var defaultName = GenerateDefaultProfileName();
            var dialog = new ProfileEditorDialog(ViewModel.Monitors, defaultName);
            dialog.XamlRoot = this.XamlRoot;

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary && dialog.ResultProfile != null)
            {
                await ViewModel.CreateProfileAsync(dialog.ResultProfile);
            }
        }

        private async void EditProfile_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuFlyoutItem;
            if (menuItem?.Tag is PowerDisplayProfile profile)
            {
                var dialog = new ProfileEditorDialog(ViewModel.Monitors, profile.Name, profile.Id);
                dialog.XamlRoot = this.XamlRoot;

                // Pre-fill with existing profile settings
                dialog.PreFillProfile(profile);

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary && dialog.ResultProfile != null)
                {
                    await ViewModel.UpdateProfileAsync(dialog.ResultProfile);
                }
            }
        }

        private async void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuFlyoutItem;
            if (menuItem?.Tag is PowerDisplayProfile profile)
            {
                var resourceLoader = ResourceLoaderInstance.ResourceLoader;
                var dialog = new ContentDialog
                {
                    XamlRoot = this.XamlRoot,
                    Title = resourceLoader.GetString("PowerDisplay_DeleteProfile_Title"),
                    Content = string.Format(System.Globalization.CultureInfo.CurrentCulture, resourceLoader.GetString("PowerDisplay_DeleteProfile_Content"), profile.DisplayName),
                    PrimaryButtonText = resourceLoader.GetString("PowerDisplay_DeleteProfile_PrimaryButton"),
                    CloseButtonText = resourceLoader.GetString("PowerDisplay_Dialog_Cancel"),
                    DefaultButton = ContentDialogButton.Close,
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    await ViewModel.DeleteProfileAsync(profile.Id);
                }
            }
        }

        private string GenerateDefaultProfileName()
        {
            var existingNames = ViewModel.Profiles.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            var baseName = resourceLoader.GetString("PowerDisplay_Profile_DefaultBaseName");
            if (string.IsNullOrEmpty(baseName))
            {
                baseName = "Profile";
            }

            if (!existingNames.Contains(baseName))
            {
                return baseName;
            }

            for (int i = 2; i < 1000; i++)
            {
                var candidate = $"{baseName} {i}";
                if (!existingNames.Contains(candidate))
                {
                    return candidate;
                }
            }

            return $"{baseName} {DateTime.Now.Ticks}";
        }

        // Custom VCP Mapping event handlers
        private async void AddCustomMapping_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CustomVcpMappingEditorDialog(ViewModel.Monitors);
            dialog.XamlRoot = this.XamlRoot;

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary && dialog.ResultMapping != null)
            {
                ViewModel.AddCustomVcpMapping(dialog.ResultMapping);
            }
        }

        private async void EditCustomMapping_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not CustomVcpValueMapping mapping)
            {
                return;
            }

            var dialog = new CustomVcpMappingEditorDialog(ViewModel.Monitors);
            dialog.XamlRoot = this.XamlRoot;
            dialog.PreFillMapping(mapping);

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary && dialog.ResultMapping != null)
            {
                ViewModel.UpdateCustomVcpMapping(mapping, dialog.ResultMapping);
            }
        }

        private async void DeleteCustomMapping_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not CustomVcpValueMapping mapping)
            {
                return;
            }

            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = resourceLoader.GetString("PowerDisplay_CustomMapping_Delete_Title"),
                Content = resourceLoader.GetString("PowerDisplay_CustomMapping_Delete_Message"),
                PrimaryButtonText = resourceLoader.GetString("Yes"),
                CloseButtonText = resourceLoader.GetString("No"),
                DefaultButton = ContentDialogButton.Close,
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                ViewModel.DeleteCustomVcpMapping(mapping);
            }
        }

        private async void EnableColorTemperature_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb || cb.Tag is not MonitorInfo monitor)
            {
                return;
            }

            await TryCommitDangerousChangeAsync(
                cb,
                cb.IsChecked == true,
                monitor.EnableColorTemperature,
                v => monitor.EnableColorTemperature = v,
                PowerDisplayWarningKind.ColorTemperature);
        }

        private async void EnablePowerState_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb || cb.Tag is not MonitorInfo monitor)
            {
                return;
            }

            await TryCommitDangerousChangeAsync(
                cb,
                cb.IsChecked == true,
                monitor.EnablePowerState,
                v => monitor.EnablePowerState = v,
                PowerDisplayWarningKind.PowerState);
        }

        private async void EnableInputSource_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb || cb.Tag is not MonitorInfo monitor)
            {
                return;
            }

            await TryCommitDangerousChangeAsync(
                cb,
                cb.IsChecked == true,
                monitor.EnableInputSource,
                v => monitor.EnableInputSource = v,
                PowerDisplayWarningKind.InputSource);
        }

        // Per-monitor CheckBoxes use OneWay binding + Click (Click only fires for real user
        // input, so the binding-driven event problem the ToggleSwitch had does not apply).
        // The "no gesture" check still appears here because Click fires for keyboard space-
        // bar even when the IsChecked didn't move, and to keep the cancel-revert path safe.
        private async Task<bool> TryCommitDangerousChangeAsync(
            CheckBox control,
            bool desiredValue,
            bool currentValue,
            Action<bool> commit,
            PowerDisplayWarningKind kind)
        {
            if (desiredValue == currentValue)
            {
                return false;
            }

            if (!desiredValue)
            {
                commit(false);
                return true;
            }

            if (await ShowDangerousFeatureDialogAsync(kind))
            {
                commit(true);
                return true;
            }

            control.IsChecked = currentValue;
            return false;
        }
    }
}
