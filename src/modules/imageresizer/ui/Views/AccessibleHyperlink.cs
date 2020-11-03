// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Documents;

namespace ImageResizer.Views
{
    public class AccessibleHyperlink : Hyperlink
    {
        public AutomationControlType ControlType { get; set; }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            var peer = new CustomizableHyperlinkAutomationPeer(this);

            peer.ControlType = ControlType;
            return peer;
        }
    }
}
