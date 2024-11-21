// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Windows.Foundation;

namespace WindowsCommandPalette.BuiltinCommands;

public partial class QuitCommandProvider : CommandProvider
{
    private readonly QuitAction quitAction = new();

    public event TypedEventHandler<object?, object?>? QuitRequested { add => quitAction.QuitRequested += value; remove => quitAction.QuitRequested -= value; }

    public override ICommandItem[] TopLevelCommands()
    {
        return [];
    }

    public override IFallbackCommandItem[] FallbackCommands()
    {
        return [new FallbackCommandItem(quitAction) { Subtitle = "Exit Command Palette" }];
    }
}
