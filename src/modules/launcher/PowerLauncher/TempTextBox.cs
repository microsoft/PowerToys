// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace PowerLauncher
{
    public class TempTextBox : TextBox
    {
        private ListView lv;

        public ListView Lv { get => lv; set => lv = value; }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new AutoSuggestTextBoxAutomationPeer(this);
        }

        internal class AutoSuggestTextBoxAutomationPeer : TextBoxAutomationPeer
        {
            public AutoSuggestTextBoxAutomationPeer(TempTextBox owner)
                : base(owner)
            {
            }

            protected override List<AutomationPeer> GetControlledPeersCore()
            {
                List<AutomationPeer> controlledPeers = new List<AutomationPeer>
                {
                    UIElementAutomationPeer.CreatePeerForElement(((TempTextBox)Owner).Lv),
                };
                return controlledPeers;
            }
        }
    }
}
