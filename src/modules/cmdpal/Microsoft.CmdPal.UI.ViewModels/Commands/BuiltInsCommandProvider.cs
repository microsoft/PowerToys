// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

/// <summary>
/// Built-in Provider for a top-level command which can quit the application. Invokes the <see cref="QuitCommand"/>, which sends a <see cref="QuitMessage"/>.
/// </summary>
public sealed partial class BuiltInsCommandProvider : CommandProvider
{
    private readonly IRootPageAccessor _rootPageAccessor;
    private readonly OpenSettingsCommand openSettings = new();
    private readonly OpenGallerySettingsCommand openGallerySettings = new();
    private readonly QuitCommand quitCommand = new();
    private readonly FallbackReloadItem _fallbackReloadItem = new();
    private readonly FallbackLogItem _fallbackLogItem;
    private readonly NewExtensionPage _newExtension = new();
    private readonly IApplicationInfoService _applicationInfoService;

    public override ICommandItem[] TopLevelCommands() =>
        [
            new CommandItem(openSettings) { },
            new CommandItem(openGallerySettings) { },
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

    public BuiltInsCommandProvider(IRootPageAccessor rootPageAccessor, IApplicationInfoService applicationInfoService)
    {
        _rootPageAccessor = rootPageAccessor;
        _applicationInfoService = applicationInfoService;
        _fallbackLogItem = new FallbackLogItem(applicationInfoService);
        Id = "com.microsoft.cmdpal.builtin.core";
        DisplayName = Properties.Resources.builtin_display_name;
        Icon = IconHelpers.FromRelativePath("Assets\\Square44x44Logo.altform-unplated_targetsize-256.png");
    }

    public override ICommandItem[]? GetDockBands()
    {
        var rootPage = _rootPageAccessor.GetRootPage();
        var logPath = _applicationInfoService.LogDirectory;
        var openLogCommand = new OpenFileCommand(logPath) { Name = Properties.Resources.builtin_log_folder_command_name };

        // The dock renders the items of the band's backing list, not the band
        // item itself, so MoreCommands must live on the inner ListItem.
        var rootItem = new ListItem(rootPage)
        {
            MoreCommands =
            [
                new CommandContextItem(openSettings) { Title = Properties.Resources.dock_settings_name },
                new CommandContextItem(new ReloadExtensionsCommand()) { Title = Properties.Resources.builtin_reload_display_title },
                new CommandContextItem(openLogCommand),
                new CommandContextItem(quitCommand) { Title = Properties.Resources.builtin_quit_subtitle },
            ],
        };

        return
        [
            new WrappedDockItem([rootItem], rootPage.Id, Properties.Resources.builtin_command_palette_title)
            {
                Icon = rootPage.Icon,
            },
        ];
    }

    public override void InitializeWithHost(IExtensionHost host) => BuiltinsExtensionHost.Instance.Initialize(host);
}
