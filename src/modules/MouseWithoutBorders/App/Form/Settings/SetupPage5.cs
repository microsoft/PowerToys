// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using MouseWithoutBorders.Core;

namespace MouseWithoutBorders
{
    public partial class SetupPage5 : SettingsFormPage
    {
        public SetupPage5()
        {
            InitializeComponent();
        }

        private void DoneButtonClick(object sender, EventArgs e)
        {
            // SendNextPage(new SettingsPage1());
            MachineStuff.CloseSetupForm();
            MachineStuff.ShowMachineMatrix();
        }
    }
}
