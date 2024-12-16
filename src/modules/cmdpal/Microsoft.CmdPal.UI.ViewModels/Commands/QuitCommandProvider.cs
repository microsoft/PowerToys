// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Microsoft.CmdPal.UI.ViewModels.Messages;

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

/// <summary>
/// Built-in Provider for a top-level command which can quit the application. Invokes the <see cref="QuitAction"/>, which sends a <see cref="QuitMessage"/>.
/// </summary>
public partial class QuitCommandProvider : CommandProvider
{
    private readonly QuitAction quitAction = new();

    public override ICommandItem[] TopLevelCommands() =>

        // HACK: fallback commands aren't wired up and we need to be able to exit
        [new FallbackCommandItem(quitAction) { Subtitle = "Exit Command Palette" }];

    public override IFallbackCommandItem[] FallbackCommands() =>
        [new FallbackCommandItem(quitAction) { Subtitle = "Exit Command Palette" }];
}
