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

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class VideoConferencePage : Page, IRefreshablePage
    {
        private VideoConferenceViewModel ViewModel { get; set; }

        private static string PickFileDialog()
        {
            // this code was changed to solve the problem with WinUI3 that prevents to select a file
            // while running elevated, when the issue is solved in WinUI3 it should be changed back
            OpenFileName openFileName = new OpenFileName();
            openFileName.StructSize = Marshal.SizeOf(openFileName);
            openFileName.Filter = "Images(*.jpg,*.jpeg,*.png)\0*.jpg;*.jpeg;*.png\0";

            // make buffer 65k bytes big as the MAX_PATH can be ~32k chars if long path is enable
            // and unicode uses 2 bytes per character
            openFileName.File = new string(new char[65000]);
            openFileName.MaxFile = openFileName.File.Length;
            openFileName.FileTitle = new string(new char[65000]);
            openFileName.MaxFileTitle = openFileName.FileTitle.Length;
            openFileName.InitialDir = null;
            openFileName.Title = string.Empty;
            openFileName.DefExt = null;

            bool result = NativeMethods.GetOpenFileName(openFileName);
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
