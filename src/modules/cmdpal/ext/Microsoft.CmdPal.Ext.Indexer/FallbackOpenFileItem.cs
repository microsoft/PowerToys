// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.Indexer;

internal sealed partial class FallbackOpenFileItem : FallbackCommandItem, System.IDisposable
{
    private static readonly NoOpCommand _baseCommandWithId = new() { Id = "com.microsoft.indexer.fallback" };

    private readonly CompositeFormat fallbackItemSearchPageTitleCompositeFormat = CompositeFormat.Parse(Resources.Indexer_fallback_searchPage_title);

    private readonly SearchEngine _searchEngine = new();

    private uint _queryCookie = 10;

    private Func<string, bool> _suppressCallback;

    public FallbackOpenFileItem()
        : base(_baseCommandWithId, Resources.Indexer_Find_Path_fallback_display_title)
    {
        Title = string.Empty;
        Subtitle = string.Empty;
        Icon = Icons.FileExplorerIcon;
    }

    public override void UpdateQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            Command = new NoOpCommand();
            Title = string.Empty;
            Subtitle = string.Empty;
            Icon = null;
            MoreCommands = null;

            return;
        }

        if (_suppressCallback is not null && _suppressCallback(query))
        {
            Command = new NoOpCommand();
            Title = string.Empty;
            Subtitle = string.Empty;
            Icon = null;
            MoreCommands = null;

            return;
        }

        if (Path.Exists(query))
        {
            // Exit 1: The query is a direct path to a file. Great! Return it.
            var item = new IndexerItem(fullPath: query);
            var listItemForUs = new IndexerListItem(item, IncludeBrowseCommand.AsDefault);
            Command = listItemForUs.Command;
            MoreCommands = listItemForUs.MoreCommands;
            Subtitle = item.FileName;
            Title = item.FullPath;
            Icon = listItemForUs.Icon;

            try
            {
                var stream = ThumbnailHelper.GetThumbnail(item.FullPath).Result;
                if (stream is not null)
                {
                    var data = new IconData(RandomAccessStreamReference.CreateFromStream(stream));
                    Icon = new IconInfo(data, data);
                }
            }
            catch
            {
            }

            return;
        }
        else
        {
            _queryCookie++;

            try
            {
                _searchEngine.Query(query, _queryCookie);
                var results = _searchEngine.FetchItems(0, 20, _queryCookie, out var _);

                if (results.Count == 0 || ((results[0] as IndexerListItem) is null))
                {
                    // Exit 2: We searched for the file, and found nothing. Oh well.
                    // Hide ourselves.
                    Title = string.Empty;
                    Subtitle = string.Empty;
                    Command = new NoOpCommand();
                    return;
                }

                if (results.Count == 1)
                {
                    // Exit 3: We searched for the file, and found exactly one thing. Awesome!
                    // Return it.
                    Title = results[0].Title;
                    Subtitle = results[0].Subtitle;
                    Icon = results[0].Icon;
                    Command = results[0].Command;
                    MoreCommands = results[0].MoreCommands;

                    return;
                }

                // Exit 4: We found more than one result. Make our command take
                // us to the file search page, prepopulated with this search.
                var indexerPage = new IndexerPage(query, _searchEngine, _queryCookie, results);
                Title = string.Format(CultureInfo.CurrentCulture, fallbackItemSearchPageTitleCompositeFormat, query);
                Icon = Icons.FileExplorerIcon;
                Command = indexerPage;

                return;
            }
            catch
            {
                Title = string.Empty;
                Subtitle = string.Empty;
                Icon = null;
                Command = new NoOpCommand();
                MoreCommands = null;
            }
        }
    }

    public void Dispose()
    {
        _searchEngine.Dispose();
        GC.SuppressFinalize(this);
    }

    public void SuppressFallbackWhen(Func<string, bool> callback)
    {
        _suppressCallback = callback;
    }
}
