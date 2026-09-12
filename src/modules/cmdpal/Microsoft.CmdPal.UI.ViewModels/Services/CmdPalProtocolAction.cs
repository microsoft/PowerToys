// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Messages;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

public abstract record CmdPalProtocolAction
{
    public sealed record RunInBackground : CmdPalProtocolAction;

    public sealed record OpenSettings(OpenSettingsMessage Message) : CmdPalProtocolAction;

    public sealed record RequestConsent(CmdPalProtocolRoute Route) : CmdPalProtocolAction;

    public sealed record Reject : CmdPalProtocolAction;
}
