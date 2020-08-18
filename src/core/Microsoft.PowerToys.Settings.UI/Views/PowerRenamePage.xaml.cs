// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Lib.ViewModels;
using Windows.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class PowerRenamePage : Page
    {
        private PowerRenameViewModel ViewModel { get; set; }

        public PowerRenamePage()
        {
            this.InitializeComponent();
            ViewModel = new PowerRenameViewModel(ShellPage.SendDefaultIPCMessage);

            DataContext = ViewModel;
        }
    }
}
