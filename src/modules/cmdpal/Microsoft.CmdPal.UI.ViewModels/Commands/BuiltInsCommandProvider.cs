// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

/// <summary>
/// Built-in Provider for a top-level command which can quit the application. Invokes the <see cref="QuitCommand"/>, which sends a <see cref="QuitMessage"/>.
/// </summary>
public sealed partial class BuiltInsCommandProvider : CommandProvider
{
    private readonly OpenSettingsCommand openSettings = new();
    private readonly QuitCommand quitCommand = new();
    private readonly FallbackReloadItem _fallbackReloadItem = new();
    private readonly FallbackLogItem _fallbackLogItem = new();
    private readonly NewExtensionPage _newExtension = new();

    private readonly Func<IPage> _rootPageFactory;

    public override ICommandItem[] TopLevelCommands() =>
        [
            new CommandItem(openSettings) { },
            new CommandItem(_newExtension) { Title = _newExtension.Title },
        ];

    public override IFallbackCommandItem[] FallbackCommands() =>
        [
            new FallbackCommandItem(
                    quitCommand,
                    Properties.Resources.builtin_quit_subtitle,
                    quitCommand.Id)
            {
                Subtitle = Properties.Resources.builtin_quit_subtitle,
            },
            _fallbackReloadItem,
            _fallbackLogItem,
        ];

    public BuiltInsCommandProvider(Func<IPage> rootPageFactory)
    {
        Id = "com.microsoft.cmdpal.builtin.core";
        DisplayName = Properties.Resources.builtin_display_name;
        Icon = IconHelpers.FromRelativePath("Assets\\Square44x44Logo.altform-unplated_targetsize-256.png");

        _rootPageFactory = rootPageFactory;
    }

    public override ICommandItem[]? GetDockBands()
    {
        var rootPage = _rootPageFactory();
        return [new WrappedDockItem(rootPage, Properties.Resources.builtin_command_palette_title)];
    }

    public override void InitializeWithHost(IExtensionHost host) => BuiltinsExtensionHost.Instance.Initialize(host);
}
