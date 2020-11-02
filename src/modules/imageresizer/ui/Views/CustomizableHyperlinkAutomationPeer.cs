// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Automation.Peers;
using System.Windows.Documents;

namespace ImageResizer.Views
{
    public class CustomizableHyperlinkAutomationPeer : HyperlinkAutomationPeer
    {
        public CustomizableHyperlinkAutomationPeer(Hyperlink owner)
            : base(owner)
        {
        }

        public AutomationControlType ControlType { get; set; }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return ControlType;
        }
    }
}
