// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Controls;

namespace Microsoft.Plugin.Shell
{
    public partial class CMDSetting : UserControl
    {
        private readonly ShellPluginSettings _settings;

        public CMDSetting(ShellPluginSettings settings)
        {
            InitializeComponent();
            _settings = settings;
        }

        private void CMDSetting_OnLoaded(object sender, RoutedEventArgs re)
        {
            ReplaceWinR.IsChecked = _settings.ReplaceWinR;
            LeaveShellOpen.IsChecked = _settings.LeaveShellOpen;
            AlwaysRunAsAdministrator.IsChecked = _settings.RunAsAdministrator;
            LeaveShellOpen.IsEnabled = _settings.Shell != ExecutionShell.RunCommand;

            LeaveShellOpen.Checked += (o, e) =>
            {
                _settings.LeaveShellOpen = true;
            };

            LeaveShellOpen.Unchecked += (o, e) =>
            {
                _settings.LeaveShellOpen = false;
            };

            AlwaysRunAsAdministrator.Checked += (o, e) =>
            {
                _settings.RunAsAdministrator = true;
            };

            AlwaysRunAsAdministrator.Unchecked += (o, e) =>
            {
                _settings.RunAsAdministrator = false;
            };

            ReplaceWinR.Checked += (o, e) =>
            {
                _settings.ReplaceWinR = true;
            };
            ReplaceWinR.Unchecked += (o, e) =>
            {
                _settings.ReplaceWinR = false;
            };

            ShellComboBox.SelectedIndex = (int)_settings.Shell;
            ShellComboBox.SelectionChanged += (o, e) =>
            {
                _settings.Shell = (ExecutionShell)ShellComboBox.SelectedIndex;
                LeaveShellOpen.IsEnabled = _settings.Shell != ExecutionShell.RunCommand;
            };
        }
    }
}
