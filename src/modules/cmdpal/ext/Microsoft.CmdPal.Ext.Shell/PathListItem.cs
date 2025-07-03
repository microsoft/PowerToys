// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell;

internal sealed partial class PathListItem : ListItem
{
    private readonly Lazy<IconInfo> _icon;
    private readonly bool _isDirectory;

    public override IIconInfo? Icon { get => _icon.Value; set => base.Icon = value; }

    public PathListItem(string path, string originalDir, Action<string>? addToHistory)
        : base(new OpenUrlWithHistoryCommand(path, addToHistory))
    {
        var fileName = Path.GetFileName(path);
        _isDirectory = Directory.Exists(path);
        if (_isDirectory)
        {
            path = path + "\\";
            fileName = fileName + "\\";
        }

        Title = fileName;
        Subtitle = path;

        // NOTE ME:
        // If there are spaces on originalDir, trim them off, BEFORE combining originalDir and fileName.
        // THEN add quotes at the end

        // Trim off leading & trailing quote, if there is one
        var trimmed = originalDir.Trim('"');
        var originalPath = Path.Combine(trimmed, fileName);
        var suggestion = originalPath;
        var hasSpace = originalPath.Contains(' ');
        if (hasSpace)
        {
            // wrap it in quotes
            suggestion = string.Concat("\"", suggestion, "\"");
        }

        TextToSuggest = suggestion;
        MoreCommands = [
            new CommandContextItem(new CopyTextCommand(path) { Name = "Copy path" }) { } // TODO:LOC
        ];

        // TODO: Follow-up during 0.4. Add the indexer commands here.
        // MoreCommands = [
        //    new CommandContextItem(new OpenWithCommand(indexerItem)),
        //    new CommandContextItem(new ShowFileInFolderCommand(indexerItem.FullPath) { Name = Resources.Indexer_Command_ShowInFolder }),
        //    new CommandContextItem(new CopyPathCommand(indexerItem)),
        //    new CommandContextItem(new OpenInConsoleCommand(indexerItem)),
        //    new CommandContextItem(new OpenPropertiesCommand(indexerItem)),
        // ];
        _icon = new Lazy<IconInfo>(() =>
        {
            var iconStream = ThumbnailHelper.GetThumbnail(path).Result;
            var icon = iconStream != null ? IconInfo.FromStream(iconStream) :
             _isDirectory ? Icons.Folder : Icons.RunV2;
            return icon;
        });
    }
}

internal sealed partial class OpenUrlWithHistoryCommand : OpenUrlCommand
{
    private readonly Action<string>? _addToHistory;
    private readonly string _url;

    public OpenUrlWithHistoryCommand(string url, Action<string>? addToHistory = null)
        : base(url)
    {
        _addToHistory = addToHistory;
        _url = url;
    }

    public override CommandResult Invoke()
    {
        _addToHistory?.Invoke(_url);
        var result = base.Invoke();
        return result;
    }
}
