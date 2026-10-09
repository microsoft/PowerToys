// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.OOBE.Enums;
using Microsoft.PowerToys.Settings.UI.OOBE.ViewModel;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    public sealed partial class OobeGrabAndMove : Page
    {
        public OobePowerToysModule ViewModel { get; set; }

        public OobeGrabAndMove()
        {
            this.InitializeComponent();
            ViewModel = App.OobeShellViewModel.GetModule(PowerToysModules.GrabAndMove);
        }

        private void SettingsLaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if (OobeWindow.OpenMainWindowCallback != null)
            {
                OobeWindow.OpenMainWindowCallback(typeof(GrabAndMovePage));
            }

            ViewModel.LogOpeningSettingsEvent();
        }
    }
}
