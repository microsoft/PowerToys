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
public partial class BuiltInsCommandProvider : CommandProvider
{
    private readonly QuitAction quitAction = new();
    private readonly ReloadExtensionsAction reloadAction = new();

    public override ICommandItem[] TopLevelCommands() => [];

    public override IFallbackCommandItem[] FallbackCommands() =>
        [
            new FallbackCommandItem(quitAction) { Subtitle = "Exit Command Palette" },
            new FallbackCommandItem(reloadAction) { Subtitle = "Reload Command Palette extensions" },
        ];

    public BuiltInsCommandProvider()
    {
        Id = "Core";
        DisplayName = "Built-in commands";
        Icon = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory.ToString(), "Assets\\StoreLogo.scale-200.png"));
    }
}
