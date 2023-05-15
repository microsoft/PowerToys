// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using MouseWithoutBorders.Class;

namespace MouseWithoutBorders
{
    public partial class SetupPage4 : SettingsFormPage
    {
        public SetupPage4()
        {
            Setting.Values.FirstRun = false;
            InitializeComponent();
        }

        private void NextButtonClick(object sender, EventArgs e)
        {
            SendNextPage(new SetupPage5());
        }
    }
}
