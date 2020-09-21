// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.Utilities;
using Microsoft.PowerToys.Settings.UI.Lib.ViewModels;
using Windows.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PowerPreviewPage : Page
    {
        public PowerPreviewViewModel ViewModel { get; set; }

        public PowerPreviewPage()
        {
            InitializeComponent();
            var settingsUtils = new SettingsUtils(new SystemIOProvider());
            ViewModel = new PowerPreviewViewModel(settingsUtils, SettingsRepository<PowerPreviewSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;
        }
    }
}
