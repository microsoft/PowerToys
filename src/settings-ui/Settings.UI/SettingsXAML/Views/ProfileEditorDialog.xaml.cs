// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.ObjectModel;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PowerDisplay.Models;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    /// <summary>
    /// Dialog for creating/editing PowerDisplay profiles
    /// </summary>
    public sealed partial class ProfileEditorDialog : ContentDialog
    {
        public ProfileEditorViewModel ViewModel { get; private set; }

        public PowerDisplayProfile? ResultProfile { get; private set; }

        public ProfileEditorDialog(
            ObservableCollection<MonitorInfo> availableMonitors,
            string defaultName = "",
            int profileId = 0)
        {
            this.InitializeComponent();
            ViewModel = new ProfileEditorViewModel(availableMonitors, defaultName, profileId);
            Closed += ProfileEditorDialog_Closed;

            // Set localized strings for ContentDialog
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            Title = resourceLoader.GetString("PowerDisplay_ProfileEditor_Title");
            PrimaryButtonText = resourceLoader.GetString("PowerDisplay_Dialog_Save");
            CloseButtonText = resourceLoader.GetString("PowerDisplay_Dialog_Cancel");
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (ViewModel.CanSave)
            {
                ResultProfile = ViewModel.CreateProfile();
            }
        }

        private void ContentDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ResultProfile = null;
        }

        private void ProfileEditorDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            ViewModel.Dispose();
        }

        /// <summary>
        /// Pre-fill the dialog with existing profile data
        /// </summary>
        public void PreFillProfile(PowerDisplayProfile profile)
        {
            ViewModel.PreFillProfile(profile);
        }
    }
}
