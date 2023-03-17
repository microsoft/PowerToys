// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace PowerLauncher
{
    public sealed class CustomSearchBox : TextBox
    {
        public List<UIElement> ControlledElements { get; } = new List<UIElement>();

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new AutoSuggestTextBoxAutomationPeer(this);
        }

        internal sealed class AutoSuggestTextBoxAutomationPeer : TextBoxAutomationPeer
        {
            public AutoSuggestTextBoxAutomationPeer(CustomSearchBox owner)
                : base(owner)
            {
            }

            protected override List<AutomationPeer> GetControlledPeersCore()
            {
                var controlledPeers = new List<AutomationPeer>();
                foreach (UIElement controlledElement in ((CustomSearchBox)Owner).ControlledElements)
                {
                    controlledPeers.Add(UIElementAutomationPeer.CreatePeerForElement(controlledElement));
                }

                return controlledPeers;
            }
        }
    }
}
