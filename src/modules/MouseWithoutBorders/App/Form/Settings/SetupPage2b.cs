// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace MouseWithoutBorders
{
    public partial class SetupPage2b : SettingsFormPage
    {
        public SetupPage2b()
        {
            InitializeComponent();
            SecurityCodeLabel.Text = GetSecureKey();
            MachineNameLabel.Text = Common.MachineName.Trim();
            BackButtonVisible = true;
            Common.ReopenSockets(true);
        }

        protected override SettingsFormPage CreateBackPage()
        {
            return new SetupPage1();
        }
    }
}
