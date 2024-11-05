// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Windows.Foundation;

namespace WindowsCommandPalette.BuiltinCommands;

public partial class ReloadExtensionsCommandProvider : CommandProvider
{
    private readonly ReloadExtensionsAction reloadAction = new();

    public override IListItem[] TopLevelCommands()
    {
        return [new ListItem(reloadAction) { Subtitle = "Reload Command Palette extensions" }];
    }
}
