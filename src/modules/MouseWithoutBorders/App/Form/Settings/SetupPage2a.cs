// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.RegularExpressions;

using MouseWithoutBorders.Class;
using MouseWithoutBorders.Core;

namespace MouseWithoutBorders
{
    public partial class SetupPage2a : SettingsFormPage
    {
        private static string _securityCode;

        protected string SecurityCode
        {
            get => _securityCode;
            set
            {
                _securityCode = value;
                SecurityCodeField.Text = value;
            }
        }

        private static string _computerName;

        protected string ComputerName
        {
            get => _computerName;
            set
            {
                _computerName = value;
                ComputerNameField.Text = value;
            }
        }

        public SetupPage2a()
        {
            InitializeComponent();
            BackButtonVisible = true;
            SecurityCodeField.Text = string.IsNullOrEmpty(SecurityCode) ? string.Empty : GetSecureKey();
            ComputerNameField.Text = string.IsNullOrEmpty(ComputerName) ? string.Empty : ComputerName;
            SetLinkButtonState();

            if (Common.RunWithNoAdminRight)
            {
                BackButtonVisible = false;
            }
        }

        protected override SettingsFormPage CreateBackPage()
        {
            return new SetupPage1();
        }

        protected override void OnLoad(System.EventArgs e)
        {
            base.OnLoad(e);
            _ = SecurityCodeField.Focus();
        }

        private void SecurityCodeFieldFieldTextChanged(object sender, System.EventArgs e)
        {
            SetLinkButtonState();
        }

        private void ComputerNameFieldFieldTextChanged(object sender, System.EventArgs e)
        {
            SetLinkButtonState();
        }

        private void SetLinkButtonState()
        {
            LinkButton.Enabled = SecurityCodeField.Text.Length >= 16 && ComputerNameField.Text.Trim().Length > 0;
            if (!string.IsNullOrEmpty(SecurityCodeField.Text))
            {
                _securityCode = SecurityCodeField.Text;
            }

            if (!string.IsNullOrEmpty(ComputerNameField.Text))
            {
                _computerName = ComputerNameField.Text;
            }
        }

        private void LinkButtonClick(object sender, System.EventArgs e)
        {
            if (GetSecureKey() != SecurityCodeField.Text)
            {
                Common.MyKey = Regex.Replace(SecurityCodeField.Text, @"\s+", string.Empty);
                SecurityCode = Common.MyKey;
            }

            MachineStuff.MachineMatrix = new string[MachineStuff.MAX_MACHINE] { ComputerNameField.Text.Trim().ToUpper(CultureInfo.CurrentCulture), Common.MachineName.Trim(), string.Empty, string.Empty };

            string[] machines = MachineStuff.MachineMatrix;
            MachineStuff.MachinePool.Initialize(machines);

            MachineStuff.UpdateMachinePoolStringSetting();
            SendNextPage(new SetupPage3a { ReturnToSettings = !Setting.Values.FirstRun });
        }

        private void ExpandHelpButtonClick(object sender, System.EventArgs e)
        {
            HelpLabel.Show();
            CollapseHelpButton.Show();
        }

        private void CollapseHelpButtonClick(object sender, System.EventArgs e)
        {
            HelpLabel.Hide();
            CollapseHelpButton.Hide();
        }
    }
}
