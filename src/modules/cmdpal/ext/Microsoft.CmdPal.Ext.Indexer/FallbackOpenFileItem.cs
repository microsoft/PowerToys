// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.CmdPal.Ext.Indexer.Helpers;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.Indexer;

internal sealed partial class FallbackOpenFileItem : FallbackCommandItem, IDisposable
{
    private const string CommandId = "com.microsoft.cmdpal.builtin.indexer.fallback";
    private static readonly NoOpCommand BaseCommandWithId = new() { Id = CommandId };

    private readonly CompositeFormat _fallbackItemSearchPageTitleFormat = CompositeFormat.Parse(Resources.Indexer_fallback_searchPage_title);
    private readonly CompositeFormat _fallbackItemSearchSubtitleMultipleResults = CompositeFormat.Parse(Resources.Indexer_Fallback_MultipleResults_Subtitle);
    private readonly Lock _queryLock = new();

    private CancellationTokenSource? _currentQueryCts;
    private Func<string, bool>? _suppressCallback;

    public FallbackOpenFileItem()
        : base(BaseCommandWithId, Resources.Indexer_Find_Path_fallback_display_title, CommandId)
    {
        Title = string.Empty;
        Subtitle = string.Empty;
        Icon = Icons.FileExplorerIcon;
    }

    public override void UpdateQuery(string query)
    {
        // Calling this will cancel any ongoing query processing. We always use a new SearchEngine
        // instance per query, as SearchEngine.Query cancels/reinitializes internally.
        CancellationToken cancellationToken;

        lock (_queryLock)
        {
            _currentQueryCts?.Cancel();
            _currentQueryCts?.Dispose();
            _currentQueryCts = new CancellationTokenSource();
            cancellationToken = _currentQueryCts.Token;

            if (string.IsNullOrWhiteSpace(query) || (_suppressCallback is not null && _suppressCallback(query)))
            {
                ClearResult();
                return;
            }
        }

        try
        {
            if (Path.Exists(query))
            {
                ProcessDirectPath(query, cancellationToken);
            }
            else
            {
                ProcessSearchQuery(query, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Query was superseded by a newer one - discard silently.
        }
        catch
        {
            lock (_queryLock)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    ClearResult();
                }
            }
        }
    }

    private void ProcessDirectPath(string query, CancellationToken ct)
    {
        var item = new IndexerItem(fullPath: query);
        var indexerListItem = new IndexerListItem(item, IncludeBrowseCommand.AsDefault);

        lock (_queryLock)
        {
            ct.ThrowIfCancellationRequested();
            UpdateResult(indexerListItem);
        }

        _ = LoadIconAsync(item.FullPath, ct);
    }

    private void ProcessSearchQuery(string query, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // for now the SearchEngine and SearchQuery are not thread-safe, so we create a new instance per query
        // since SearchEngine will re-initialize on a new query anyway, it doesn't seem to be a big overhead for now
        var searchEngine = new SearchEngine();

        try
        {
            var count = searchEngine.Query(query, queryCookie: 0);
            ct.ThrowIfCancellationRequested();

            lock (_queryLock)
            {
                ct.ThrowIfCancellationRequested();

                if (count == 0)
                {
                    ClearResult();
                }
                else if (count == 1)
                {
                    var results = searchEngine.FetchItems(0, 1, queryCookie: 0, out _);
                    if (results.Count > 0 && results[0] is IndexerListItem indexerListItem)
                    {
                        UpdateResult(indexerListItem);
                    }
                    else
                    {
                        ClearResult();
                    }
                }
                else
                {
                    // Ownership of searchEngine transfers to IndexerPage
                    var indexerPage = new IndexerPage(query, searchEngine, disposeSearchEngine: true);
                    searchEngine = null;

                    UpdateResult(
                        string.Format(CultureInfo.CurrentCulture, _fallbackItemSearchPageTitleFormat, query),
                        string.Format(CultureInfo.CurrentCulture, _fallbackItemSearchSubtitleMultipleResults, count),
                        Icons.FileExplorerIcon,
                        indexerPage,
                        MoreCommands,
                        DataPackage);
                }
            }
        }
        finally
        {
            searchEngine?.Dispose();
        }
    }

    private void UpdateResult(IndexerListItem listItem)
    {
        UpdateResult(listItem.Title, listItem.Subtitle, listItem.Icon, listItem.Command, listItem.MoreCommands, DataPackageHelper.CreateDataPackageForPath(listItem, listItem.FilePath));
    }

    private void ClearResult()
    {
        UpdateResult(string.Empty, string.Empty, Icons.FileExplorerIcon, BaseCommandWithId, null, null);
    }

    private void UpdateResult(string title, string subtitle, IIconInfo? iconInfo, ICommand? command, IContextItem[]? moreCommands, DataPackage? dataPackage)
    {
        Title = title;
        Subtitle = subtitle;
        Icon = iconInfo;
        Command = command;
        MoreCommands = moreCommands!;
        DataPackage = dataPackage;
    }

    private async Task LoadIconAsync(string path, CancellationToken ct)
    {
        try
        {
            var stream = await ThumbnailHelper.GetThumbnail(path).ConfigureAwait(false);
            if (stream is null || ct.IsCancellationRequested)
            {
                return;
            }

            var thumbnailStream = RandomAccessStreamReference.CreateFromStream(stream);
            if (ct.IsCancellationRequested)
            {
                return;
            }

            var data = new IconData(thumbnailStream);
            Icon = new IconInfo(data);
        }
        catch
        {
            // ignore - keep default icon
            Icon = Icons.FileExplorerIcon;
        }
    }

    public void Dispose()
    {
        _currentQueryCts?.Cancel();
        _currentQueryCts?.Dispose();
        GC.SuppressFinalize(this);
    }

    public void SuppressFallbackWhen(Func<string, bool> callback)
    {
        _suppressCallback = callback;
    }
}
