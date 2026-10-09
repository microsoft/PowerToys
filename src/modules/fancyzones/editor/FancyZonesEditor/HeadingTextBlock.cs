// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace FancyZonesEditor
{
    public class HeadingTextBlock : TextBlock
    {
        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new HeadingTextBlockAutomationPeer(this);
        }

        internal sealed class HeadingTextBlockAutomationPeer : TextBlockAutomationPeer
        {
            public HeadingTextBlockAutomationPeer(HeadingTextBlock owner)
                : base(owner)
            {
            }

            protected override AutomationControlType GetAutomationControlTypeCore()
            {
                return AutomationControlType.Header;
            }
        }
    }
}
