// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class CheckUpdateControl : UserControl
    {
        public UpdateViewModel ViewModel => ShellPage.ShellHandler?.UpdateViewModel;

        public CheckUpdateControl()
        {
            InitializeComponent();
#if POWERTOYS_LOCAL_E2E_BUILD
            IsEnabled = false;
            ToolTipService.SetToolTip(this, "Software updates are disabled in the Workspaces Local E2E build.");
#endif
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            ShellPage.ShellHandler?.OpenUpdateActivity();
        }
    }
}
