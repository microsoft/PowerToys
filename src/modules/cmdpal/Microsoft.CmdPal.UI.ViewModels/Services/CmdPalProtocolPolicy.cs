// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Services;

public static class CmdPalProtocolPolicy
{
    /// <summary>Maps a parsed route to its UI activation action.</summary>
    public static CmdPalProtocolAction Evaluate(CmdPalProtocolRoute route) => route switch
    {
        CmdPalProtocolRoute.Background => new CmdPalProtocolAction.RunInBackground(),
        CmdPalProtocolRoute.OpenSettings openSettings => new CmdPalProtocolAction.OpenSettings(openSettings.Message),
        CmdPalProtocolRoute.Reload or CmdPalProtocolRoute.ExecuteCommand => new CmdPalProtocolAction.RequestConsent(route),
        _ => new CmdPalProtocolAction.Reject(),
    };
}
