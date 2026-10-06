// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class DisplayProfilesPage : NavigablePage
    {
        private MonitorPowerViewModel ViewModel { get; } = new();

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

        private void ClearSelection_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearPreviewSelection();
        }

        private void PreviewDisplay_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: MonitorDisplayInfo display })
            {
                ViewModel.TogglePreviewDisplaySelection(display);
            }
        }

        private static bool TryGetProfile(object sender, out ProfileInfo profile)
        {
            if (sender is FrameworkElement { DataContext: ProfileInfo fromContext })
            {
                profile = fromContext;
                return true;
            }

            if (sender is FrameworkElement { Tag: ProfileInfo fromTag })
            {
                profile = fromTag;
                return true;
            }

            profile = null!;
            return false;
        }

        private async void ApplyProfile_Click(object sender, RoutedEventArgs e)
        {
            if (TryGetProfile(sender, out var profile))
            {
                ViewModel.SelectedProfile = profile;
                await ViewModel.ApplyProfileAsync();
            }
        }

        private async void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            var nameInput = new TextBox
            {
                PlaceholderText = GetLocalizedString("DisplayProfiles_ProfileName_Placeholder", "Profile name"),
            };
            AutomationProperties.SetName(nameInput, GetLocalizedString("DisplayProfiles_ProfileName_AccessibilityName", "Profile name"));
            var validationMessage = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed,
            };
            var content = new StackPanel { Spacing = 8 };
            content.Children.Add(nameInput);
            content.Children.Add(validationMessage);

            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = GetLocalizedString("DisplayProfiles_SaveProfileDialog_Title", "Save display profile"),
                Content = content,
                PrimaryButtonText = GetLocalizedString("DisplayProfiles_SaveProfileDialog_Save", "Save"),
                CloseButtonText = GetLocalizedString("DisplayProfiles_SaveProfileDialog_Cancel", "Cancel"),
                DefaultButton = ContentDialogButton.Primary,
            };
            dialog.PrimaryButtonClick += (_, args) =>
            {
                if (!ViewModel.TryValidateProfileName(nameInput.Text, out var message))
                {
                    validationMessage.Text = message;
                    validationMessage.Visibility = Visibility.Visible;
                    args.Cancel = true;
                }
            };
            nameInput.TextChanged += (_, _) => validationMessage.Visibility = Visibility.Collapsed;

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await ViewModel.SaveProfileAsync(nameInput.Text);
            }
        }

        private async void TestController_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.TestControllerAsync();
        }

        private bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);

        private async void CaptureControllerShortcut_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.CaptureControllerShortcutAsync();
        }

        private void ResetControllerShortcut_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ResetControllerShortcut();
        }

        private void OpenDiagnosticsLog_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.OpenDiagnosticsLog();
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (TryGetProfile(sender, out var profile))
            {
                ViewModel.SelectedProfile = profile;
                _ = ConfirmDeleteProfileAsync(profile);
            }
        }

        private async void RenameProfile_Click(object sender, RoutedEventArgs e)
        {
            if (TryGetProfile(sender, out var profile) &&
                await PromptForProfileNameAsync("Rename profile", profile.Name) is { } newName)
            {
                await ViewModel.RenameProfileAsync(profile, newName);
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

        private static string GetLocalizedString(string key, string fallback)
        {
            var value = ResourceLoaderInstance.ResourceLoader.GetString(key);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }
}
