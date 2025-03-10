// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

/// <summary>
/// Built-in Provider for a top-level command which can quit the application. Invokes the <see cref="QuitCommand"/>, which sends a <see cref="QuitMessage"/>.
/// </summary>
public partial class BuiltInsCommandProvider : CommandProvider
{
    private readonly OpenSettingsCommand openSettings = new();
    private readonly QuitCommand quitCommand = new();
    private readonly FallbackReloadItem _fallbackReloadItem = new();
    private readonly FallbackLogItem _fallbackLogItem = new();
    private readonly NewExtensionPage _newExtension = new();

    public override ICommandItem[] TopLevelCommands() =>
        [
            new CommandItem(openSettings) { Subtitle = "Open Command Palette settings" },
            new CommandItem(_newExtension) { Title = _newExtension.Title, Subtitle = "Creates a project for a new Command Palette extension" },
        ];

    public override IFallbackCommandItem[] FallbackCommands() =>
        [
            new FallbackCommandItem(quitCommand, displayTitle: "Exit Command Palette") { Subtitle = "Exit Command Palette" },
            _fallbackReloadItem,
            _fallbackLogItem,
        ];

    public BuiltInsCommandProvider()
    {
        Id = "Core";
        DisplayName = "Built-in commands";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.scale-200.png");
    }

    public override void InitializeWithHost(IExtensionHost host) => BuiltinsExtensionHost.Instance.Initialize(host);
}
