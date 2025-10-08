// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.Common.Commands;
using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace Microsoft.CmdPal.Ext.Shell;

internal sealed partial class PathListItem : ListItem
{
    private readonly Lazy<bool> fetchedIcon;
    private readonly bool isDirectory;
    private readonly string path;

    public override IIconInfo? Icon { get => fetchedIcon.Value ? _icon : _icon; set => base.Icon = value; }

    private IIconInfo? _icon;

    internal bool IsDirectory => isDirectory;

    public PathListItem(string path, string originalDir, Action<string>? addToHistory, ITelemetryService? telemetryService = null)
        : base(new OpenUrlWithHistoryCommand(path, addToHistory, telemetryService))
    {
        var fileName = Path.GetFileName(path);
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = Path.GetFileName(Path.GetDirectoryName(path)) ?? string.Empty;
        }

        isDirectory = Directory.Exists(path);
        if (isDirectory)
        {
            if (!path.EndsWith('\\'))
            {
                path = path + "\\";
            }

            if (!fileName.EndsWith('\\'))
            {
                fileName = fileName + "\\";
            }
        }

        this.path = path;

        Title = fileName; // Just the name of the file is the Title
        Subtitle = path; // What the user typed is the subtitle

        TextToSuggest = path;

        MoreCommands = [
            new CommandContextItem(new OpenWithCommand(path)),
            new CommandContextItem(new ShowFileInFolderCommand(path)) { RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: VirtualKey.E) },
            new CommandContextItem(new CopyPathCommand(path)) { RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: VirtualKey.C) },
            new CommandContextItem(new OpenInConsoleCommand(path)) { RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: VirtualKey.R) },
            new CommandContextItem(new OpenPropertiesCommand(path)),
         ];

        fetchedIcon = new Lazy<bool>(() =>
        {
            _ = Task.Run(FetchIconAsync);
            return true;
        });
    }

    private async Task FetchIconAsync()
    {
        var iconStream = await ThumbnailHelper.GetThumbnail(path);
        var icon = iconStream != null ?
            IconInfo.FromStream(iconStream) :
            isDirectory ? Icons.FolderIcon : Icons.RunV2Icon;
        _icon = icon;
        OnPropertyChanged(nameof(Icon));
    }
}
