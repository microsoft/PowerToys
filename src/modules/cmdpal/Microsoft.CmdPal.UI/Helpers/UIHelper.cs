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
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Microsoft.CmdPal.UI.Helpers;

public static partial class UIHelper
{
    static UIHelper()
    {
    }

    internal static void PreparePopupForShow(FlyoutBase popup, FrameworkElement placementTarget)
    {
        if (placementTarget.XamlRoot is not null && popup.XamlRoot != placementTarget.XamlRoot)
        {
            popup.XamlRoot = placementTarget.XamlRoot;
        }
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
