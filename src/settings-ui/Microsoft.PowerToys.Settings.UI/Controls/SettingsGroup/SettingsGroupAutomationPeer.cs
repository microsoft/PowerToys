// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Automation.Peers;

namespace Microsoft.PowerToys.Settings.UI.Controls.SettingsGroup
{
    public class SettingsGroupAutomationPeer : FrameworkElementAutomationPeer
    {
        public SettingsGroupAutomationPeer(SettingsGroup owner)
            : base(owner)
        {
        }

        protected override string GetNameCore()
        {
            var selectedSettingsGroup = (SettingsGroup)Owner;
            return selectedSettingsGroup.Header;
        }
    }
}
