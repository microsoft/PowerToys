// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Common.Search.SemanticSearch;

/// <summary>
/// Adapts the SemanticSearchEngine to the generic ISearchEngine interface.
/// </summary>
/// <typeparam name="T">The type of items to search.</typeparam>
public sealed class SemanticSearchAdapter<T> : ISearchEngine<T>
    where T : ISearchable
{
    private readonly SemanticSearchEngine _engine;
    private readonly Dictionary<string, T> _itemsById = new();
    private readonly object _lockObject = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SemanticSearchAdapter{T}"/> class.
    /// </summary>
    /// <param name="indexName">The name of the search index.</param>
    public SemanticSearchAdapter(string indexName)
    {
        _engine = new SemanticSearchEngine(indexName);
    }

    /// <inheritdoc/>
    public bool IsReady => _engine.IsInitialized;

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
    public SemanticSearchCapabilities? SemanticCapabilities => _engine.Capabilities;

    /// <summary>
    /// Occurs when the semantic search capabilities change.
    /// </summary>
    public event EventHandler<SemanticSearchCapabilities>? CapabilitiesChanged
    {
        add => _engine.CapabilitiesChanged += value;
        remove => _engine.CapabilitiesChanged -= value;
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _engine.InitializeAsync();
    }

    /// <inheritdoc/>
    public Task IndexAsync(T item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ThrowIfDisposed();

        var text = BuildSearchableText(item);
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.CompletedTask;
        }

        lock (_lockObject)
        {
            _itemsById[item.Id] = item;
        }

        _engine.IndexText(item.Id, text);
        return Task.CompletedTask;
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

        _engine.IndexTextBatch(batch);
        return Task.CompletedTask;
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

        _engine.Remove(id);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        lock (_lockObject)
        {
            _itemsById.Clear();
        }

        _engine.RemoveAll();
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
            return Task.FromResult<IReadOnlyList<SearchResult<T>>>(Array.Empty<SearchResult<T>>());
        }

        options ??= new SearchOptions();

        var semanticOptions = new SemanticSearchOptions
        {
            MaxResults = options.MaxResults,
            Language = options.Language,
            MatchScope = SemanticSearchMatchScope.Unconstrained,
            TextMatchType = SemanticSearchTextMatchType.Fuzzy,
        };

        var matches = _engine.SearchText(query, semanticOptions);
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

        return Task.FromResult<IReadOnlyList<SearchResult<T>>>(results);
    }

    /// <summary>
    /// Waits for the indexing process to complete.
    /// </summary>
    /// <param name="timeout">The maximum time to wait.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task WaitForIndexingCompleteAsync(TimeSpan timeout)
    {
        ThrowIfDisposed();
        await _engine.WaitForIndexingCompleteAsync(timeout);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _engine.Dispose();

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
