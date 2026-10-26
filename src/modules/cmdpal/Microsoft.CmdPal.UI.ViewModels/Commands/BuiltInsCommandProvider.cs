// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

/// <summary>
/// Provides Command Palette's built-in commands and dock band.
/// </summary>
public sealed partial class BuiltInsCommandProvider : CommandProvider
{
    private readonly IRootPageAccessor _rootPageAccessor;
    private readonly OpenSettingsCommand _openSettings = new();
    private readonly OpenGallerySettingsCommand _openGallerySettings = new();
    private readonly QuitCommand _quitCommand = new();
    private readonly ReloadExtensionsCommand _reloadCommand = new();
    private readonly LogCommandItem _logItem;
    private readonly NewExtensionPage _newExtension = new();
    private readonly IApplicationInfoService _applicationInfoService;

    public override ICommandItem[] TopLevelCommands() =>
        [
            new CommandItem(_openSettings) { },
            new CommandItem(_openGallerySettings) { },
            new CommandItem(_newExtension) { Title = _newExtension.Title },
            new CommandItem(_quitCommand) { Title = Properties.Resources.builtin_quit_title },
            new CommandItem(_reloadCommand) { Title = Properties.Resources.builtin_reload_title },
            _logItem,
        ];

    public BuiltInsCommandProvider(IRootPageAccessor rootPageAccessor, IApplicationInfoService applicationInfoService)
    {
        _rootPageAccessor = rootPageAccessor;
        _applicationInfoService = applicationInfoService;
        _logItem = new LogCommandItem(applicationInfoService);
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
                new CommandContextItem(_openSettings) { Title = Properties.Resources.dock_settings_name },
                new CommandContextItem(openLogCommand),
                new Separator(),
                new CommandContextItem(_reloadCommand) { Title = Properties.Resources.builtin_reload_display_title },
                new Separator(),
                new CommandContextItem(_quitCommand) { Title = Properties.Resources.builtin_quit_title },
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
