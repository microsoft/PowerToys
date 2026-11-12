// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.PowerToys.Settings.UI.Controls;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class SoundManagerPage : NavigablePage, IRefreshablePage
    {
        private SoundManagerViewModel ViewModel { get; set; }

        private readonly ResourceLoader resourceLoader = ResourceLoaderInstance.ResourceLoader;

        public SoundManagerPage()
        {
            var settingsUtils = SettingsUtils.Default;
            ViewModel = new SoundManagerViewModel(
                settingsUtils,
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                SettingsRepository<SoundManagerSettings>.GetInstance(settingsUtils),
                ShellPage.SendDefaultIPCMessage);

            DataContext = ViewModel;
            InitializeComponent();

            Loaded += async (s, e) =>
            {
                ViewModel.OnPageLoaded();
                await ViewModel.LoadIconsAsync();
                await ViewModel.LoadDuckingAsync();
            };
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }

        // Opens the list of the programs that are making sound, so that one of them can be given a shortcut of its own.
        private async void AddMuteShortcutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var programs = await ViewModel.GetProgramsAsync(CancellationToken.None);

                var picker = new SoundManagerProgramPicker();
                picker.SetPrograms(programs);

                var dialog = new ContentDialog
                {
                    XamlRoot = RootPage.XamlRoot,
                    Title = resourceLoader.GetString("SoundManager_MuteShortcuts_PickerTitle"),
                    Content = picker,
                    PrimaryButtonText = resourceLoader.GetString("SoundManager_MuteShortcuts_PickerAdd"),
                    CloseButtonText = resourceLoader.GetString("SoundManager_MuteShortcuts_PickerCancel"),
                    DefaultButton = ContentDialogButton.Close,
                    IsPrimaryButtonEnabled = false,
                };

                picker.ProgramPicked += (s, program) => dialog.IsPrimaryButtonEnabled = true;
                picker.SelectionCleared += (s, e) => dialog.IsPrimaryButtonEnabled = false;

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary && picker.SelectedProgram != null)
                {
                    await ViewModel.AddMuteShortcutAsync(picker.SelectedProgram);
                }
            }
            catch (Exception)
            {
                // The list of the programs could not be shown, so there is nothing to add.
            }
        }

        private void RemoveMuteShortcutButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is SoundManagerMuteShortcutItem item)
            {
                ViewModel.RemoveMuteShortcut(item);
            }
        }
    }
}
