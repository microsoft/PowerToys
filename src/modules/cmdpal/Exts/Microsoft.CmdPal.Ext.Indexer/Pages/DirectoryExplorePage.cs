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

/// <summary>
/// This is almost more of just a sample than anything.
/// This is one singular page for switching.
/// </summary>
public sealed partial class DirectoryExplorePage : DynamicListPage
{
    private string _path;
    private List<ExploreListItem>? _directoryContents;
    private List<ExploreListItem>? _filteredContents;

    public DirectoryExplorePage(string path)
    {
        _path = path;
        Icon = Icons.FileExplorer;
        Name = Resources.Indexer_Command_Browse;
        Title = path;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (_directoryContents == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(newSearch))
        {
            if (_filteredContents != null)
            {
                _filteredContents = null;
                RaiseItemsChanged(-1);
            }

            return;
        }

        // Need to break this out the manual way so that the compiler can know
        // this is an ExploreListItem
        var filteredResults = ListHelpers.FilterList(
            _directoryContents,
            newSearch,
            (s, i) => ListHelpers.ScoreListItem(s, i));

        if (_filteredContents != null)
        {
            lock (_filteredContents)
            {
                ListHelpers.InPlaceUpdateList<ExploreListItem>(_filteredContents, filteredResults);
            }
        }
        else
        {
            _filteredContents = filteredResults.ToList();
        }

        RaiseItemsChanged(-1);
    }

    public override IListItem[] GetItems()
    {
        if (_filteredContents != null)
        {
            return _filteredContents.ToArray();
        }

        if (_directoryContents != null)
        {
            return _directoryContents.ToArray();
        }

        IsLoading = true;
        if (!Path.Exists(_path))
        {
            EmptyContent = new CommandItem(title: Resources.Indexer_File_Does_Not_Exist);
            return [];
        }

        var attr = File.GetAttributes(_path);

        // detect whether its a directory or file
        if ((attr & FileAttributes.Directory) != FileAttributes.Directory)
        {
            EmptyContent = new CommandItem(title: Resources.Indexer_File_Is_File_Not_Folder);
            return [];
        }

        var contents = Directory.EnumerateFileSystemEntries(_path);
        _directoryContents = contents
            .Select(s => new IndexerItem() { FullPath = s, FileName = Path.GetFileName(s) })
            .Select(i => new ExploreListItem(i))
            .ToList();

        foreach (var i in _directoryContents)
        {
            i.PathChangeRequested += HandlePathChangeRequested;
        }

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

        IsLoading = false;

        return _directoryContents.ToArray();
    }

    private void HandlePathChangeRequested(ExploreListItem sender, string path)
    {
        _directoryContents = null;
        _filteredContents = null;
        _path = path;
        Title = path;
        SearchText = string.Empty;
        RaiseItemsChanged(-1);
    }
}
