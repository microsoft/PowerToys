// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;

namespace Microsoft.CmdPal.UI.Helpers;

public static partial class UIHelper
{
    static UIHelper()
    {
    }

    public static void AnnounceActionForAccessibility(UIElement ue, string announcement, string activityID)
    {
        if (FrameworkElementAutomationPeer.FromElement(ue) is AutomationPeer peer)
        {
            peer.RaiseNotificationEvent(
                AutomationNotificationKind.ActionCompleted,
                AutomationNotificationProcessing.ImportantMostRecent,
                announcement,
                activityID);
        }
    }
}
