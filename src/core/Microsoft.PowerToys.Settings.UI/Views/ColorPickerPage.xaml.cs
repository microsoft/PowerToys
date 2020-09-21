// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.Utilities;
using Microsoft.PowerToys.Settings.UI.Lib.ViewModels;
using Windows.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class ColorPickerPage : Page
    {
        public ColorPickerViewModel ViewModel { get; set; }

        public ColorPickerPage()
        {
            var settingsUtils = new SettingsUtils(new SystemIOProvider());
            ViewModel = new ColorPickerViewModel(settingsUtils, ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;
            InitializeComponent();
        }
    }
}
