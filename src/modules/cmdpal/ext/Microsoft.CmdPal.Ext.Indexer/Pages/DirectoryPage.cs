// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Storage.Streams;

#nullable enable
namespace Microsoft.CmdPal.Ext.Indexer;

public sealed partial class DirectoryPage : ListPage
{
    private readonly string _path;

    private List<IndexerListItem>? _directoryContents;

    public DirectoryPage(string path)
    {
        _path = path;
        Icon = Icons.FileExplorer;
        Name = Resources.Indexer_Command_Browse;
        Title = path;
    }

    public override IListItem[] GetItems()
    {
        if (_directoryContents != null)
        {
            return _directoryContents.ToArray();
        }

        if (!Path.Exists(_path))
        {
            EmptyContent = new CommandItem(
                title: Resources.Indexer_File_Does_Not_Exist,
                subtitle: $"{_path}");
            return [];
        }

        var attr = File.GetAttributes(_path);

        // detect whether its a directory or file
        if ((attr & FileAttributes.Directory) != FileAttributes.Directory)
        {
            EmptyContent = new CommandItem(
                title: Resources.Indexer_File_Is_File_Not_Folder, subtitle: $"{_path}")
            {
                Icon = Icons.Document,
            };
            return [];
        }

        var contents = Directory.EnumerateFileSystemEntries(_path);

        if (!contents.Any())
        {
            var item = new IndexerItem() { FullPath = _path, FileName = Path.GetFileName(_path) };
            var listItemForUs = new IndexerListItem(item, IncludeBrowseCommand.Exclude);
            EmptyContent = new CommandItem(
                title: Resources.Indexer_Folder_Is_Empty, subtitle: $"{_path}")
            {
                Icon = Icons.FolderOpen,
                Command = listItemForUs.Command,
                MoreCommands = listItemForUs.MoreCommands,
            };
            return [];
        }

        _directoryContents = contents
            .Select(s => new IndexerItem() { FullPath = s, FileName = Path.GetFileName(s) })
            .Select(i => new IndexerListItem(i, IncludeBrowseCommand.AsDefault))
            .ToList();

        _ = Task.Run(() =>
        {
            foreach (var item in _directoryContents)
            {
                IconInfo? icon = null;
                try
                {
                    var stream = ThumbnailHelper.GetThumbnail(item.FilePath).Result;
                    if (stream != null)
                    {
                        var data = new IconData(RandomAccessStreamReference.CreateFromStream(stream));
                        icon = new IconInfo(data, data);
                    }
                }
                catch
                {
                }

                item.Icon = icon;
            }
        });

        return _directoryContents.ToArray();
    }
}
