// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class VideoConferencePage : Page, IRefreshablePage
    {
        private VideoConferenceViewModel ViewModel { get; set; }

        [DllImport("Comdlg32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetOpenFileName([In, Out] OpenFileName openFileName);

        private static async Task<string> PickFileDialog()
        {
            OpenFileName openFileName = new OpenFileName();
            openFileName.StructSize = Marshal.SizeOf(openFileName);
            openFileName.Filter = "Images (*.jpg, *.jpeg, *.png)\0*.jpg; *.jpeg; *.png\0";
            openFileName.File = new string(new char[1024]);
            openFileName.MaxFile = openFileName.File.Length;
            openFileName.FileTitle = new string(new char[1024]);
            openFileName.MaxFileTitle = openFileName.FileTitle.Length;
            openFileName.InitialDir = null;
            openFileName.Title = string.Empty;
            openFileName.DefExt = null;

            await Task.Delay(10);

            bool result = GetOpenFileName(openFileName);
            if (result)
            {
                return openFileName.File;
            }

            return null;
        }

        public VideoConferencePage()
        {
            var settingsUtils = new SettingsUtils();
            ViewModel = new VideoConferenceViewModel(
                settingsUtils,
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                SettingsRepository<VideoConferenceSettings>.GetInstance(settingsUtils),
                ShellPage.SendDefaultIPCMessage,
                PickFileDialog);
            DataContext = ViewModel;
            InitializeComponent();
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }
    }
}
