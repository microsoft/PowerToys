// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.CmdPal.Ext.Indexer.Helpers;
using Microsoft.CmdPal.Ext.Indexer.Indexer;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.ApplicationModel.DataTransfer;

namespace Microsoft.CmdPal.Ext.Indexer;

internal sealed partial class FallbackOpenFileItem : FallbackCommandItem, IDisposable
{
    private const string CommandId = "com.microsoft.cmdpal.builtin.indexer.fallback";

    // Cookie to identify our queries; since we replace the SearchEngine on each search,
    // this can be a constant.
    private const uint HardQueryCookie = 10;
    private static readonly NoOpCommand BaseCommandWithId = new() { Id = CommandId };

    private readonly CompositeFormat _fallbackItemSearchSubtitleFormat = CompositeFormat.Parse(Resources.Indexer_fallback_searchPage_title);
    private readonly Lock _querySwitchLock = new();
    private readonly Lock _resultLock = new();

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
        UpdateQueryCore(query);
    }

    private void UpdateQueryCore(string query)
    {
        // Calling this will cancel any ongoing query processing. We always use a new SearchEngine
        // instance per query, as SearchEngine.Query cancels/reinitializes internally.
        CancellationToken cancellationToken;

        lock (_querySwitchLock)
        {
            _currentQueryCts?.Cancel();
            _currentQueryCts?.Dispose();
            _currentQueryCts = new CancellationTokenSource();
            cancellationToken = _currentQueryCts.Token;
        }

        var suppressCallback = _suppressCallback;
        if (string.IsNullOrWhiteSpace(query) || (suppressCallback is not null && suppressCallback(query)))
        {
            ClearResultForCurrentQuery(cancellationToken);
            return;
        }

        try
        {
            var exists = Path.Exists(query);
            if (exists)
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
            if (!cancellationToken.IsCancellationRequested)
            {
                ClearResultForCurrentQuery(cancellationToken);
            }
        }
    }

    private void ProcessDirectPath(string query, CancellationToken ct)
    {
        var item = new IndexerItem(fullPath: query);
        var indexerListItem = new IndexerListItem(item, IncludeBrowseCommand.AsDefault);

        ct.ThrowIfCancellationRequested();
        UpdateResultForCurrentQuery(indexerListItem, ct);
    }

    private void ProcessSearchQuery(string query, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // for now the SearchEngine and SearchQuery are not thread-safe, so we create a new instance per query
        // since SearchEngine will re-initialize on a new query anyway, it doesn't seem to be a big overhead for now
        var searchEngine = new SearchEngine();

        try
        {
            searchEngine.Query(query, queryCookie: HardQueryCookie);
            ct.ThrowIfCancellationRequested();

            // We only need to know whether there are 0, 1, or more than one result
            var results = searchEngine.FetchItems(0, 2, queryCookie: HardQueryCookie, out _, out var notice);
            var count = results.Count;

            if (count == 0)
            {
                if (notice is { } searchNotice)
                {
                    UpdateSearchNoticeForCurrentQuery(query, searchNotice, ct);
                }
                else
                {
                    ClearResultForCurrentQuery(ct);
                }
            }
            else if (count == 1)
            {
                if (results[0] is IndexerListItem indexerListItem)
                {
                    UpdateResultForCurrentQuery(indexerListItem, ct);
                }
                else
                {
                    ClearResultForCurrentQuery(ct);
                }
            }
            else
            {
                var indexerPage = new IndexerPage(query);

                var set = UpdateResultForCurrentQuery(
                    Resources.IndexerCommandsProvider_DisplayName,
                    string.Format(CultureInfo.CurrentCulture, _fallbackItemSearchSubtitleFormat, query),
                    Icons.FileExplorerIcon,
                    indexerPage,
                    MoreCommands,
                    DataPackage,
                    ct);

                if (!set)
                {
                    // if we failed to set the result (query was cancelled), dispose the page and search engine
                    indexerPage.Dispose();
                }
            }
        }
        finally
        {
            searchEngine?.Dispose();
        }
    }

    private bool ClearResultForCurrentQuery(CancellationToken ct)
    {
        return UpdateResultForCurrentQuery(string.Empty, string.Empty, Icons.FileExplorerIcon, BaseCommandWithId, null, null, ct);
    }

    private bool UpdateResultForCurrentQuery(IndexerListItem listItem, CancellationToken ct)
    {
        return UpdateResultForCurrentQuery(
            listItem.Title,
            listItem.Subtitle,
            listItem.Icon,
            listItem.Command,
            listItem.MoreCommands,
            DataPackageHelper.CreateDataPackageForPath(listItem, listItem.FilePath),
            ct);
    }

    private bool UpdateResultForCurrentQuery(string title, string subtitle, IIconInfo? iconInfo, ICommand? command, IContextItem[]? moreCommands, DataPackage? dataPackage, CancellationToken ct)
    {
        lock (_resultLock)
        {
            if (ct.IsCancellationRequested)
            {
                return false;
            }

            Title = title;
            Subtitle = subtitle;
            Icon = iconInfo ?? Icons.FileExplorerIcon;

            MoreCommands = moreCommands!;
            DataPackage = dataPackage;
            Command = command;
            return true;
        }
    }

    private bool UpdateSearchNoticeForCurrentQuery(string query, SearchNoticeInfo notice, CancellationToken ct)
    {
        var (title, subtitle) = GetFallbackNoticeText(notice);
        var indexerPage = new IndexerPage(query);
        var set = UpdateResultForCurrentQuery(
            title,
            subtitle,
            Icons.FileExplorerIcon,
            indexerPage,
            [
                new CommandContextItem(new OpenUrlCommand("ms-settings:search") { Name = Resources.Indexer_Command_OpenIndexerSettings! }),
            ],
            null,
            ct);

        if (!set)
        {
            indexerPage.Dispose();
        }

        return set;
    }

    internal static (string Title, string Subtitle) GetFallbackNoticeText(SearchNoticeInfo notice)
    {
        return (Resources.IndexerCommandsProvider_DisplayName!, notice.Title);
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
