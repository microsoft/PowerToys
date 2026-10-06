// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class DisplayProfilesPage : NavigablePage
    {
        private MonitorPowerViewModel ViewModel { get; } = new();

        private bool _capturingActivationShortcut;

        public DisplayProfilesPage()
        {
            var initializationTimer = Stopwatch.StartNew();
            Logger.LogInfo("Creating Monitor Power Settings page.");
            DataContext = ViewModel;
            try
            {
                InitializeComponent();
                ViewModel.PreviewDisplays.CollectionChanged += PreviewDisplays_CollectionChanged;
                Loaded += DisplayProfilesPage_Loaded;
                Logger.LogInfo($"Monitor Power Settings page XAML initialized in {initializationTimer.ElapsedMilliseconds} ms.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize the Monitor Power Settings page.", ex);
                throw;
            }

            Logger.LogInfo("Monitor Power Settings page created.");
        }

        private void PreviewDisplays_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RenderTopologyPreview();
        }

        private void RenderTopologyPreview()
        {
            if (Resources["PreviewTileTemplate"] is not DataTemplate previewTileTemplate)
            {
                throw new InvalidOperationException("The Monitor Power preview tile template is unavailable.");
            }

            DisplayTopologyCanvas.Children.Clear();
            foreach (var display in ViewModel.PreviewDisplays)
            {
                if (previewTileTemplate.LoadContent() is not FrameworkElement tile)
                {
                    throw new InvalidOperationException("The Monitor Power preview tile template did not create a UI element.");
                }

                tile.DataContext = display;
                Canvas.SetLeft(tile, display.LayoutLeft);
                Canvas.SetTop(tile, display.LayoutTop);
                DisplayTopologyCanvas.Children.Add(tile);
            }
        }

        private void DisplayProfilesPage_Loaded(object sender, RoutedEventArgs e)
        {
            Logger.LogInfo("Monitor Power Settings page Loaded event fired; starting data initialization.");
            ViewModel.OnPageLoaded();
        }

        private void CaptureActivationShortcut_Click(object sender, RoutedEventArgs e)
        {
            _capturingActivationShortcut = true;
            ViewModel.StatusMessage = GetResourceString(
                "DisplayProfiles_ActivationShortcut_CapturePrompt",
                "Press a modifier and key combination. Press Escape to cancel.");
        }

        private void DisplayProfilesPage_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!_capturingActivationShortcut)
            {
                return;
            }

            e.Handled = true;
            if (e.Key == VirtualKey.Escape)
            {
                _capturingActivationShortcut = false;
                ViewModel.StatusMessage = GetResourceString(
                    "DisplayProfiles_ActivationShortcut_CaptureCancelled",
                    "Shortcut capture cancelled.");
                return;
            }

            if (e.Key is VirtualKey.Control or VirtualKey.Menu or VirtualKey.Shift or VirtualKey.LeftWindows or VirtualKey.RightWindows)
            {
                return;
            }

            var modifiers = new System.Collections.Generic.List<string>();
            if (IsKeyDown(VirtualKey.LeftWindows) || IsKeyDown(VirtualKey.RightWindows))
            {
                modifiers.Add("Win");
            }

            if (IsKeyDown(VirtualKey.Control))
            {
                modifiers.Add("Ctrl");
            }

            if (IsKeyDown(VirtualKey.Menu))
            {
                modifiers.Add("Alt");
            }

            if (IsKeyDown(VirtualKey.Shift))
            {
                modifiers.Add("Shift");
            }

            if (modifiers.Count == 0)
            {
                ViewModel.StatusMessage = GetResourceString(
                    "DisplayProfiles_ActivationShortcut_ModifierRequired",
                    "The shortcut needs at least one modifier key.");
                return;
            }

            var keyName = e.Key switch
            {
                VirtualKey.Enter => "Enter",
                VirtualKey.Space => "Space",
                _ => e.Key.ToString(),
            };
            modifiers.Add(keyName);
            _capturingActivationShortcut = false;
            ViewModel.ActivationShortcut = string.Join(" + ", modifiers);
        }

        private static bool IsKeyDown(VirtualKey key)
            => InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);

        private static string GetResourceString(string resourceKey, string fallback)
        {
            var value = ResourceLoaderInstance.ResourceLoader.GetString(resourceKey);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private async void ApplyProfile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: ProfileInfo profile })
            {
                ViewModel.SelectedProfile = profile;
                await ViewModel.ApplyProfileAsync();
            }
        }

        private async void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            bool overwrite = false;
            if (ViewModel.ProfileExists(ViewModel.NewProfileName))
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    Title = "Overwrite saved profile?",
                    Content = $"A profile named '{ViewModel.NewProfileName}' already exists. Replace it with the current display selection?",
                    PrimaryButtonText = "Overwrite",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                };
                overwrite = await dialog.ShowAsync() == ContentDialogResult.Primary;
                if (!overwrite)
                {
                    return;
                }
            }

            await ViewModel.SaveProfileAsync(overwrite);
        }

        private async void TestController_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.TestControllerAsync();
        }

        private void RefreshDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.RefreshDiagnostics();
        }

        private async void RestoreBaseTopology_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Restore base display topology?",
                Content = "Windows will change the active displays and their layout. If the change fails, the current topology remains available for recovery with Escape.",
                PrimaryButtonText = "Restore",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                ViewModel.RestoreBaseTopology();
            }
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: ProfileInfo profile })
            {
                ViewModel.SelectedProfile = profile;
                _ = ConfirmDeleteProfileAsync(profile);
            }
        }

        private async void OverwriteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: ProfileInfo profile })
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    Title = "Overwrite profile?",
                    Content = $"Replace '{profile.Name}' with the currently selected displays and layout?",
                    PrimaryButtonText = "Overwrite",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                };

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    await ViewModel.OverwriteProfileAsync(profile);
                }
            }
        }

        private async void RenameProfile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: ProfileInfo profile } &&
                await PromptForProfileNameAsync("Rename profile", profile.Name) is { } newName)
            {
                await ViewModel.RenameProfileAsync(profile, newName);
            }
        }

        private async void DuplicateProfile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: ProfileInfo profile } &&
                await PromptForProfileNameAsync("Duplicate profile", $"{profile.Name} Copy") is { } newName)
            {
                await ViewModel.DuplicateProfileAsync(profile, newName);
            }
        }

        private async Task ConfirmDeleteProfileAsync(ProfileInfo profile)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Delete profile?",
                Content = $"Delete '{profile.Name}'? This cannot be undone.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                ViewModel.DeleteProfile();
            }
        }

        private async Task<string?> PromptForProfileNameAsync(string title, string initialName)
        {
            var nameInput = new TextBox
            {
                Text = initialName,
                PlaceholderText = "Profile name",
            };
            AutomationProperties.SetName(nameInput, "Profile name");
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = title,
                Content = nameInput,
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
            };

            return await dialog.ShowAsync() == ContentDialogResult.Primary
                ? nameInput.Text
                : null;
        }
    }
}
