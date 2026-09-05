// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Common;
using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.CmdPal.Ext.Indexer.Indexer;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Indexer;

internal sealed partial class IndexerPage : DynamicListPage, IDisposable
{
    // Cookie to identify our queries; since we replace the SearchEngine on each search,
    // this can be a constant.
    private const uint HardQueryCookie = 10;

    private readonly List<IListItem> _indexerListItems = [];
    private readonly Lock _searchLock = new();
    private readonly Lock _queryLock = new();
    private readonly Func<IIndexerSearchEngine> _createSearchEngine;
    private readonly IndexerThumbnailLoader _thumbnails;

    private IIndexerSearchEngine? _searchEngine;

    private CancellationTokenSource? _searchCts;
    private string _initialQuery = string.Empty;
    private bool _isEmptyQuery = true;

    private CommandItem? _noSearchEmptyContent;
    private CommandItem? _nothingFoundEmptyContent;
    private CommandItem? _noticeEmptyContent;
    private ListItem? _noticeListItem;
    private SearchNoticeInfo? _currentNotice;

    private bool _deferredLoad;
    private bool _disposed;

    internal Task SearchTask { get; private set; } = Task.CompletedTask;

    internal Task ThumbnailTask => _thumbnails.Completion;

    public override ICommandItem EmptyContent => _isEmptyQuery ? _noSearchEmptyContent! : _currentNotice is null ? _nothingFoundEmptyContent! : _noticeEmptyContent!;

    public IndexerPage()
        : this(() => new SearchEngine())
    {
        Id = BuiltInCommandIds.FileSearch;
        PlaceholderText = Resources.Indexer_PlaceholderText;
    }

    internal IndexerPage(
        Func<IIndexerSearchEngine> createSearchEngine,
        Func<string, CancellationToken, Task<IconInfo?>>? loadThumbnail = null)
    {
        _createSearchEngine = createSearchEngine;
        _thumbnails = new(_searchLock, loadThumbnail);
        Icon = Icons.FileExplorerIcon;
        Name = Resources.Indexer_Title;

        var filters = new SearchFilters();
        filters.PropChanged += Filters_PropChanged;
        Filters = filters;

        CreateEmptyContent();
    }

    public IndexerPage(string query)
        : this(() => new SearchEngine())
    {
        _initialQuery = query;
        SearchText = query;
        IsLoading = true;
        _deferredLoad = true;
    }

    private void CreateEmptyContent()
    {
        _noSearchEmptyContent = new CommandItem(new NoOpCommand())
        {
            Icon = Icon,
            Subtitle = Resources.Indexer_NoSearchQueryMessageTip,
        };

        _nothingFoundEmptyContent = new CommandItem(new AnonymousCommand(StartManualSearch) { Name = Resources.Indexer_Command_SearchAllFiles! })
        {
            Icon = Icon,
            Title = Resources.Indexer_NoResultsMessage,
            Subtitle = Resources.Indexer_NoResultsMessageTip,
            MoreCommands = [
                new CommandContextItem(new OpenUrlCommand("ms-settings:search") { Name = Resources.Indexer_Command_OpenIndexerSettings! })
                {
                    Title = Resources.Indexer_Command_SearchAllFiles!,
                },
                ],
        };

        _noticeEmptyContent = new CommandItem(new OpenUrlCommand("ms-settings:search") { Name = Resources.Indexer_Command_OpenIndexerSettings! })
        {
            Icon = Icon,
        };

        _noticeListItem = new ListItem(new NoOpCommand())
        {
            Icon = Icon,
            MoreCommands = [
                new CommandContextItem(new OpenUrlCommand("ms-settings:search") { Name = Resources.Indexer_Command_OpenIndexerSettings! }),
                ],
        };
    }

    private void StartManualSearch()
    {
        // {20D04FE0-3AEA-1069-A2D8-08002B30309D} is CLSID for "This PC"
        const string template = "search-ms:query={0}&crumb=location:::{{20D04FE0-3AEA-1069-A2D8-08002B30309D}}";
        var fullSearchText = FullSearchString(SearchText);
        var encodedSearchText = UrlEncoder.Default.Encode(fullSearchText);
        var command = string.Format(CultureInfo.CurrentCulture, template, encodedSearchText);
        ShellHelpers.OpenInShell(command);
    }

    private void Filters_PropChanged(object sender, IPropChangedEventArgs args)
    {
        PerformSearch(SearchText);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (oldSearch != newSearch && newSearch != _initialQuery)
        {
            PerformSearch(newSearch);
        }
    }

    public override IListItem[] GetItems()
    {
        lock (_searchLock)
        {
            if (_deferredLoad && !_disposed)
            {
                _deferredLoad = false;
                PerformSearch(_initialQuery);
            }

            return _currentNotice is null
                ? [.. _indexerListItems]
                : [_noticeListItem!, .. _indexerListItems];
        }
    }

    private string FullSearchString(string query)
    {
        switch (Filters?.CurrentFilterId)
        {
            case "folders":
                return $"System.Kind:folders {query}";
            case "files":
                return $"System.Kind:NOT folders {query}";
            case "all":
            default:
                return query;
        }
    }

    public override void LoadMore()
    {
        CancellationToken ct;
        lock (_searchLock)
        {
            if (_disposed || _searchCts is null || !HasMoreItems)
            {
                return;
            }

            ct = _searchCts.Token;
        }

        LoadMore(ct);
    }

    private void LoadMore(CancellationToken ct)
    {
        try
        {
            lock (_queryLock)
            {
                IIndexerSearchEngine? searchEngine;
                int offset;
                lock (_searchLock)
                {
                    if (_disposed || ct.IsCancellationRequested || !HasMoreItems)
                    {
                        return;
                    }

                    searchEngine = _searchEngine;
                    offset = _indexerListItems.Count;
                    IsLoading = true;
                }

                if (searchEngine is null)
                {
                    return;
                }

                var results = searchEngine.FetchItems(offset, 20, HardQueryCookie, out var hasMore, out var notice, ct);

                lock (_searchLock)
                {
                    if (_disposed || ct.IsCancellationRequested)
                    {
                        return;
                    }

                    // Icon reads are our demand signal; extensions do not receive viewport visibility.
                    foreach (var result in results)
                    {
                        if (result is IndexerListItem item)
                        {
                            var thumbnails = _thumbnails;
                            item.LoadThumbnailOnDemand(resultItem => thumbnails.Request(resultItem, ct));
                        }
                    }

                    ApplyNotice(notice);
                    _indexerListItems.AddRange(results);
                    HasMoreItems = hasMore;
                    IsLoading = false;
                    RaiseItemsChanged(GetVisibleItemCount());
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ReportSearchFailure(ex, ct);
        }
    }

    private void PerformSearch(string newSearch)
    {
        var actualSearch = FullSearchString(newSearch);

        lock (_searchLock)
        {
            if (_disposed)
            {
                return;
            }

            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _searchCts = new();
            var ct = _searchCts.Token;
            _thumbnails.ClearPending();
            _indexerListItems.Clear();
            ApplyNotice(null);
            _isEmptyQuery = string.IsNullOrWhiteSpace(newSearch);
            _initialQuery = _isEmptyQuery ? string.Empty : newSearch;
            HasMoreItems = false;
            IsLoading = !_isEmptyQuery;
            RaiseItemsChanged(0);
            OnPropertyChanged(nameof(EmptyContent));
            var isEmptyQuery = _isEmptyQuery;
            SearchTask = Task.Run(() => RunSearch(actualSearch, isEmptyQuery, ct));
        }
    }

    private void RunSearch(string query, bool isEmptyQuery, CancellationToken ct)
    {
        try
        {
            lock (_queryLock)
            {
                ct.ThrowIfCancellationRequested();
                _searchEngine?.Dispose();
                _searchEngine = null;
                if (isEmptyQuery)
                {
                    return;
                }

                var searchEngine = _createSearchEngine();
                _searchEngine = searchEngine;
                ct.ThrowIfCancellationRequested();
                var notice = searchEngine.Query(query, HardQueryCookie);

                lock (_searchLock)
                {
                    ct.ThrowIfCancellationRequested();
                    ApplyNotice(notice);
                    HasMoreItems = true;
                }

                LoadMore(ct);
                ct.ThrowIfCancellationRequested();
                lock (_searchLock)
                {
                    ct.ThrowIfCancellationRequested();
                    OnPropertyChanged(nameof(EmptyContent));
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ReportSearchFailure(ex, ct);
        }
    }

    private void ReportSearchFailure(Exception exception, CancellationToken ct)
    {
        Logger.LogError("Failed to search for files.", exception);
        lock (_searchLock)
        {
            if (!ct.IsCancellationRequested)
            {
                ApplyNotice(new(Resources.Indexer_SearchFailedMessage!, Resources.Indexer_SearchFailedMessageTip!));
                HasMoreItems = false;
                IsLoading = false;
                RaiseItemsChanged(GetVisibleItemCount());
                OnPropertyChanged(nameof(EmptyContent));
            }
        }
    }

    public void Dispose()
    {
        lock (_searchLock)
        {
            _disposed = true;
            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _searchCts = null;
            _thumbnails.Dispose();
            _indexerListItems.Clear();
            ApplyNotice(null);
            HasMoreItems = false;
            IsLoading = false;
        }

        lock (_queryLock)
        {
            _searchEngine?.Dispose();
            _searchEngine = null;
        }

        GC.SuppressFinalize(this);
    }

    private void ApplyNotice(SearchNoticeInfo? notice)
    {
        _currentNotice = notice;
        if (notice is null)
        {
            return;
        }

        _noticeEmptyContent!.Title = notice.Value.Title;
        _noticeEmptyContent.Subtitle = notice.Value.Subtitle;

        _noticeListItem!.Title = notice.Value.Title;
        _noticeListItem.Subtitle = notice.Value.Subtitle;
    }

    private int GetVisibleItemCount() => _indexerListItems.Count + (_currentNotice is null ? 0 : 1);
}
