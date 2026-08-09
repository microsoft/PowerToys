// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Messages;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

public abstract record CmdPalProtocolRoute
{
    public sealed record Background : CmdPalProtocolRoute;

    public sealed record OpenSettings(OpenSettingsMessage Message) : CmdPalProtocolRoute;

    public sealed record Reload : CmdPalProtocolRoute;
}
