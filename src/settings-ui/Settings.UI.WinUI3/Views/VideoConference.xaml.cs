// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Windows.Storage;
using Windows.Storage.Pickers;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.WinUI3.Views
{
    public sealed partial class VideoConferencePage : Page
    {
        private VideoConferenceViewModel ViewModel { get; set; }

        private static async Task<string> PickFileDialog()
        {
            FileOpenPicker openPicker = new FileOpenPicker();
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            openPicker.FileTypeFilter.Add(".jpg");
            openPicker.FileTypeFilter.Add(".jpeg");
            openPicker.FileTypeFilter.Add(".png");
            ((IInitializeWithWindow)(object)openPicker).Initialize(System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle);

            StorageFile file = await openPicker.PickSingleFileAsync();
            return file?.Path;
        }

        public VideoConferencePage()
        {
            var settingsUtils = new SettingsUtils();
            ViewModel = new VideoConferenceViewModel(settingsUtils, SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage, PickFileDialog);
            DataContext = ViewModel;
            InitializeComponent();
        }
    }
}
