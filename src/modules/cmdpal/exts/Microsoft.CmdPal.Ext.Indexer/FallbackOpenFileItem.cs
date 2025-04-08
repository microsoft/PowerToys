// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.CmdPal.Ext.Indexer.Indexer;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.Indexer;

internal sealed partial class FallbackOpenFileItem : FallbackCommandItem, System.IDisposable
{
    private readonly SettingsManager _settingsManager;

    private readonly CompositeFormat fallbackItemSearchPageTitleCompositeFormat = CompositeFormat.Parse(Resources.Indexer_fallback_searchPage_title);

    private SearchEngine _searchEngine = new();

    private uint _queryCookie = 10;

    public FallbackOpenFileItem(SettingsManager settingsManager)
        : base(new NoOpCommand(), Resources.Indexer_Find_Path_fallback_display_title)
    {
        Title = string.Empty;
        Subtitle = string.Empty;
        _settingsManager = settingsManager;
    }

    public override void UpdateQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            Title = string.Empty;
            Subtitle = string.Empty;
            Icon = null;
            Command = new NoOpCommand();
            MoreCommands = null;

            return;
        }

        if (_settingsManager.FallbackCommandModeSettings == SettingsManager.FallbackCommandMode.FilePathExist)
        {
            if (Path.Exists(query))
            {
                var item = new IndexerItem() { FullPath = query, FileName = Path.GetFileName(query) };
                var listItemForUs = new IndexerListItem(item, IncludeBrowseCommand.AsDefault);
                Command = listItemForUs.Command;
                MoreCommands = listItemForUs.MoreCommands;
                Subtitle = item.FileName;
                Title = item.FullPath;
                Icon = listItemForUs.Icon;

                try
                {
                    var stream = ThumbnailHelper.GetThumbnail(item.FullPath).Result;
                    if (stream != null)
                    {
                        var data = new IconData(RandomAccessStreamReference.CreateFromStream(stream));
                        Icon = new IconInfo(data, data);
                    }
                }
                catch
                {
                }
            }
            else
            {
                Title = string.Empty;
                Subtitle = string.Empty;
                Icon = null;
                Command = new NoOpCommand();
                MoreCommands = null;
            }
        }

        if (_settingsManager.FallbackCommandModeSettings == SettingsManager.FallbackCommandMode.AlwaysOn)
        {
            _queryCookie++;

            try
            {
                _searchEngine.Query(query, _queryCookie);
                var results = _searchEngine.FetchItems(0, 20, _queryCookie, out var _);
                if (results.Count == 0 || (results[0] as IndexerListItem == null))
                {
                    Title = string.Empty;
                    Subtitle = string.Empty;
                    Command = new NoOpCommand();
                    return;
                }

                if (results.Count == 1)
                {
                    Title = results[0].Title;
                    Subtitle = results[0].Subtitle;
                    Icon = results[0].Icon;
                    Command = results[0].Command;
                    MoreCommands = results[0].MoreCommands;
                    return;
                }

                var indexerPage = new IndexerPage(_settingsManager, query, _searchEngine, _queryCookie, results);
                Title = string.Format(CultureInfo.CurrentCulture, fallbackItemSearchPageTitleCompositeFormat, query);
                Icon = Icons.FileExplorer;
                Subtitle = Resources.Indexer_Subtitle;
                Command = indexerPage;
            }
            catch
            {
            }
        }
    }

    public void Dispose()
    {
        _searchEngine.Dispose();
        GC.SuppressFinalize(this);
    }
}
