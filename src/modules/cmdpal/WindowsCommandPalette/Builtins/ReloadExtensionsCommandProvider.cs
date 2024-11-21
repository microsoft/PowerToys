// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace WindowsCommandPalette.BuiltinCommands;

public partial class ReloadExtensionsCommandProvider : CommandProvider
{
    private readonly ReloadExtensionsAction reloadAction = new();

    public override ICommandItem[] TopLevelCommands()
    {
        return [];
    }

    public override IFallbackCommandItem[] FallbackCommands()
    {
        return [new FallbackCommandItem(reloadAction) { Subtitle = "Reload Command Palette extensions" }];
    }
}
