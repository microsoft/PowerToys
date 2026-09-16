// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Automation.Peers;

namespace PowerOCR.Views;

internal sealed partial class OverlayPageAutomationPeer : FrameworkElementAutomationPeer
{
    public OverlayPageAutomationPeer(OverlayPage owner)
        : base(owner)
    {
    }

    protected override string GetClassNameCore() => nameof(OverlayPage);

    protected override AutomationControlType GetAutomationControlTypeCore()
        => AutomationControlType.Pane;
}
