// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using MouseWithoutBorders.Core;

namespace MouseWithoutBorders
{
    public partial class SetupPage1 : SettingsFormPage
    {
        public SetupPage1()
        {
            InitializeComponent();

            MachineStuff.ClearComputerMatrix();
        }

        private void NoButtonClick(object sender, EventArgs e)
        {
            SendNextPage(new SetupPage2b());
        }

        private void YesButtonClick(object sender, EventArgs e)
        {
            SendNextPage(new SetupPage2aa());
        }
    }
}
