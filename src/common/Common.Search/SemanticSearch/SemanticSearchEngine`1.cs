// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;

namespace Common.Search.SemanticSearch;

/// <summary>
/// A semantic search engine that implements the common search interface.
/// </summary>
/// <typeparam name="T">The type of items to search.</typeparam>
public sealed class SemanticSearchEngine<T> : ISearchEngine<T>
    where T : ISearchable
{
    private readonly SemanticSearchIndex _index;
    private readonly Dictionary<string, T> _itemsById = new();
    private readonly object _lockObject = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SemanticSearchEngine{T}"/> class.
    /// </summary>
    /// <param name="indexName">The name of the search index.</param>
    public SemanticSearchEngine(string indexName)
    {
        Logger.LogDebug($"[SemanticSearchEngine] Creating engine. IndexName={indexName}, ItemType={typeof(T).Name}");
        _index = new SemanticSearchIndex(indexName);
    }

    /// <inheritdoc/>
    public bool IsReady => _index.IsInitialized;

    /// <inheritdoc/>
    public SearchEngineCapabilities Capabilities { get; } = new()
    {
        SupportsFuzzyMatch = true,
        SupportsSemanticSearch = true,
        PersistsIndex = true,
        SupportsIncrementalIndex = true,
        SupportsMatchSpans = false,
    };

    /// <summary>
    /// Gets the underlying semantic search capabilities.
    /// </summary>
    public SemanticSearchCapabilities? SemanticCapabilities => _index.Capabilities;

    /// <summary>
    /// Gets the last error that occurred during a search operation, or null if no error occurred.
    /// </summary>
    public SearchError? LastError => _index.LastError;

    /// <summary>
    /// Occurs when the semantic search capabilities change.
    /// </summary>
    public event EventHandler<SemanticSearchCapabilities>? CapabilitiesChanged
    {
        add => _index.CapabilitiesChanged += value;
        remove => _index.CapabilitiesChanged -= value;
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Logger.LogInfo($"[SemanticSearchEngine] InitializeAsync starting. ItemType={typeof(T).Name}");
        var result = await _index.InitializeAsync().ConfigureAwait(false);

        if (result.IsFailure)
        {
            Logger.LogWarning($"[SemanticSearchEngine] InitializeAsync failed. ItemType={typeof(T).Name}, Error={result.Error?.Message}");
        }
        else
        {
            Logger.LogInfo($"[SemanticSearchEngine] InitializeAsync completed. ItemType={typeof(T).Name}");
        }

        // Note: We don't throw here to maintain backward compatibility,
        // but callers can check LastError for details if initialization failed.
    }

    /// <summary>
    /// Initializes the search engine and returns the result with error details if any.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or failure with error details.</returns>
    public async Task<SearchOperationResult> InitializeWithResultAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return await _index.InitializeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task IndexAsync(T item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ThrowIfDisposed();

        var text = BuildSearchableText(item);
        if (string.IsNullOrWhiteSpace(text))
        {
            Logger.LogDebug($"[SemanticSearchEngine] IndexAsync skipped (empty text). Id={item.Id}");
            return Task.CompletedTask;
        }

        lock (_lockObject)
        {
            _itemsById[item.Id] = item;
        }

        Logger.LogDebug($"[SemanticSearchEngine] IndexAsync. Id={item.Id}, TextLength={text.Length}");

        // Note: Errors are captured in LastError for external logging
        _ = _index.IndexText(item.Id, text);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Indexes a single item and returns the result with error details if any.
    /// </summary>
    /// <param name="item">The item to index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or failure with error details.</returns>
    public Task<SearchOperationResult> IndexWithResultAsync(T item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ThrowIfDisposed();

        var text = BuildSearchableText(item);
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(SearchOperationResult.Success());
        }

        lock (_lockObject)
        {
            _itemsById[item.Id] = item;
        }

        return Task.FromResult(_index.IndexText(item.Id, text));
    }

    /// <inheritdoc/>
    public Task IndexBatchAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ThrowIfDisposed();

        var batch = new List<(string Id, string Text)>();

        lock (_lockObject)
        {
            foreach (var item in items)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Logger.LogDebug($"[SemanticSearchEngine] IndexBatchAsync cancelled. ItemsProcessed={batch.Count}");
                    break;
                }

                var text = BuildSearchableText(item);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    _itemsById[item.Id] = item;
                    batch.Add((item.Id, text));
                }
            }
        }

        Logger.LogInfo($"[SemanticSearchEngine] IndexBatchAsync. BatchSize={batch.Count}");

        // Note: Errors are captured in LastError for external logging
        _ = _index.IndexTextBatch(batch);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Indexes multiple items in batch and returns the result with error details if any.
    /// </summary>
    /// <param name="items">The items to index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or failure with error details.</returns>
    public Task<SearchOperationResult> IndexBatchWithResultAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ThrowIfDisposed();

        var batch = new List<(string Id, string Text)>();

        lock (_lockObject)
        {
            foreach (var item in items)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var text = BuildSearchableText(item);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    _itemsById[item.Id] = item;
                    batch.Add((item.Id, text));
                }
            }
        }

        return Task.FromResult(_index.IndexTextBatch(batch));
    }

    /// <inheritdoc/>
    public Task RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ThrowIfDisposed();

        lock (_lockObject)
        {
            _itemsById.Remove(id);
        }

        Logger.LogDebug($"[SemanticSearchEngine] RemoveAsync. Id={id}");

        // Note: Errors are captured in LastError for external logging
        _ = _index.Remove(id);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        int count;
        lock (_lockObject)
        {
            count = _itemsById.Count;
            _itemsById.Clear();
        }

        Logger.LogInfo($"[SemanticSearchEngine] ClearAsync. ItemsCleared={count}");

        // Note: Errors are captured in LastError for external logging
        _ = _index.RemoveAll();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<SearchResult<T>>> SearchAsync(
        string query,
        SearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(query))
        {
            Logger.LogDebug($"[SemanticSearchEngine] SearchAsync skipped (empty query).");
            return Task.FromResult<IReadOnlyList<SearchResult<T>>>(Array.Empty<SearchResult<T>>());
        }

        options ??= new SearchOptions();
        Logger.LogDebug($"[SemanticSearchEngine] SearchAsync starting. Query={query}, MaxResults={options.MaxResults}");

        var semanticOptions = new SemanticSearchOptions
        {
            MaxResults = options.MaxResults,
            Language = options.Language,
            MatchScope = SemanticSearchMatchScope.Unconstrained,
            TextMatchType = SemanticSearchTextMatchType.Fuzzy,
        };

        var searchResult = _index.SearchText(query, semanticOptions);

        // Note: Errors are captured in LastError for external logging
        var matches = searchResult.Value ?? Array.Empty<SemanticSearchResult>();
        var results = new List<SearchResult<T>>();

        lock (_lockObject)
        {
            foreach (var match in matches)
            {
                if (_itemsById.TryGetValue(match.ContentId, out var item))
                {
                    results.Add(new SearchResult<T>
                    {
                        Item = item,
                        Score = 100.0, // Semantic search doesn't return scores, use fixed value
                        MatchKind = SearchMatchKind.Semantic,
                        MatchSpans = null,
                    });
                }
            }
        }

        Logger.LogDebug($"[SemanticSearchEngine] SearchAsync completed. Query={query}, Matches={matches.Count}, Results={results.Count}");
        return Task.FromResult<IReadOnlyList<SearchResult<T>>>(results);
    }

    /// <summary>
    /// Searches for items matching the query and returns the result with error details if any.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="options">Optional search options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing search results or error details.</returns>
    public Task<SearchOperationResult<IReadOnlyList<SearchResult<T>>>> SearchWithResultAsync(
        string query,
        SearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(SearchOperationResult<IReadOnlyList<SearchResult<T>>>.Success(Array.Empty<SearchResult<T>>()));
        }

        options ??= new SearchOptions();

        var semanticOptions = new SemanticSearchOptions
        {
            MaxResults = options.MaxResults,
            Language = options.Language,
            MatchScope = SemanticSearchMatchScope.Unconstrained,
            TextMatchType = SemanticSearchTextMatchType.Fuzzy,
        };

        var searchResult = _index.SearchText(query, semanticOptions);
        var matches = searchResult.Value ?? Array.Empty<SemanticSearchResult>();
        var results = new List<SearchResult<T>>();

        lock (_lockObject)
        {
            foreach (var match in matches)
            {
                if (_itemsById.TryGetValue(match.ContentId, out var item))
                {
                    results.Add(new SearchResult<T>
                    {
                        Item = item,
                        Score = 100.0,
                        MatchKind = SearchMatchKind.Semantic,
                        MatchSpans = null,
                    });
                }
            }
        }

        if (searchResult.IsFailure)
        {
            return Task.FromResult(SearchOperationResult<IReadOnlyList<SearchResult<T>>>.FailureWithFallback(searchResult.Error!, results));
        }

        return Task.FromResult(SearchOperationResult<IReadOnlyList<SearchResult<T>>>.Success(results));
    }

    /// <summary>
    /// Waits for the indexing process to complete.
    /// </summary>
    /// <param name="timeout">The maximum time to wait.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task WaitForIndexingCompleteAsync(TimeSpan timeout)
    {
        ThrowIfDisposed();
        await _index.WaitForIndexingCompleteAsync(timeout).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Logger.LogDebug($"[SemanticSearchEngine] Disposing. ItemType={typeof(T).Name}");
        _index.Dispose();

        lock (_lockObject)
        {
            _itemsById.Clear();
        }

        _disposed = true;
    }

    private static string BuildSearchableText(T item)
    {
        var primary = item.SearchableText ?? string.Empty;
        var secondary = item.SecondarySearchableText;

        if (string.IsNullOrWhiteSpace(secondary))
        {
            return primary;
        }

        return $"{primary} {secondary}";
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
