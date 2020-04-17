// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class PowerRenamePage : Page
    {
        public PowerRenameViewModel ViewModel { get; set; }

        public PowerRenamePage()
        {
            this.InitializeComponent();

            ViewModel = new PowerRenameViewModel();
            this.PowerRenameSettingsView.DataContext = ViewModel;
        }
    }
}
