// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace Common.Search.FuzzSearch;

/// <summary>
/// A search engine that uses fuzzy string matching for search.
/// </summary>
/// <typeparam name="T">The type of items to search.</typeparam>
public sealed class FuzzSearchEngine<T> : ISearchEngine<T>
    where T : ISearchable
{
    private readonly object _lockObject = new();
    private readonly Dictionary<string, T> _itemsById = new();
    private readonly Dictionary<string, (string PrimaryNorm, string? SecondaryNorm)> _normalizedCache = new();
    private bool _isReady;
    private bool _disposed;

    /// <inheritdoc/>
    public bool IsReady
    {
        get
        {
            lock (_lockObject)
            {
                return _isReady;
            }
        }
    }

    /// <inheritdoc/>
    public SearchEngineCapabilities Capabilities { get; } = new()
    {
        SupportsFuzzyMatch = true,
        SupportsSemanticSearch = false,
        PersistsIndex = false,
        SupportsIncrementalIndex = true,
        SupportsMatchSpans = true,
    };

    /// <inheritdoc/>
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        lock (_lockObject)
        {
            _isReady = true;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task IndexAsync(T item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ThrowIfDisposed();

        lock (_lockObject)
        {
            _itemsById[item.Id] = item;
            _normalizedCache[item.Id] = (
                NormalizeString(item.SearchableText),
                item.SecondarySearchableText != null ? NormalizeString(item.SecondarySearchableText) : null
            );
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task IndexBatchAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ThrowIfDisposed();

        lock (_lockObject)
        {
            foreach (var item in items)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                _itemsById[item.Id] = item;
                _normalizedCache[item.Id] = (
                    NormalizeString(item.SearchableText),
                    item.SecondarySearchableText != null ? NormalizeString(item.SecondarySearchableText) : null
                );
            }
        }

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
            _normalizedCache.Remove(id);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        lock (_lockObject)
        {
            _itemsById.Clear();
            _normalizedCache.Clear();
        }

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
        var normalizedQuery = NormalizeString(query);

        List<KeyValuePair<string, T>> snapshot;
        lock (_lockObject)
        {
            if (_itemsById.Count == 0)
            {
                return Task.FromResult<IReadOnlyList<SearchResult<T>>>(Array.Empty<SearchResult<T>>());
            }

            snapshot = _itemsById.ToList();
        }

        var bag = new ConcurrentBag<SearchResult<T>>();
        var po = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1),
        };

        try
        {
            Parallel.ForEach(snapshot, po, kvp =>
            {
                var (primaryNorm, secondaryNorm) = GetNormalizedTexts(kvp.Key);

                var primaryResult = StringMatcher.FuzzyMatch(normalizedQuery, primaryNorm);
                double score = primaryResult.Score;
                List<int>? matchData = primaryResult.MatchData;

                if (!string.IsNullOrEmpty(secondaryNorm))
                {
                    var secondaryResult = StringMatcher.FuzzyMatch(normalizedQuery, secondaryNorm);
                    if (secondaryResult.Success && secondaryResult.Score * 0.8 > score)
                    {
                        score = secondaryResult.Score * 0.8;
                        matchData = null; // Secondary matches don't have primary text spans
                    }
                }

                if (score > options.MinScore)
                {
                    var result = new SearchResult<T>
                    {
                        Item = kvp.Value,
                        Score = score,
                        MatchKind = SearchMatchKind.Fuzzy,
                        MatchSpans = options.IncludeMatchSpans && matchData != null
                            ? ConvertToMatchSpans(matchData)
                            : null,
                    };

                    bag.Add(result);
                }
            });
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult<IReadOnlyList<SearchResult<T>>>(Array.Empty<SearchResult<T>>());
        }

        var results = bag
            .OrderByDescending(r => r.Score)
            .Take(options.MaxResults)
            .ToList();

        return Task.FromResult<IReadOnlyList<SearchResult<T>>>(results);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_lockObject)
        {
            _itemsById.Clear();
            _normalizedCache.Clear();
            _isReady = false;
        }

        _disposed = true;
    }

    private (string PrimaryNorm, string? SecondaryNorm) GetNormalizedTexts(string id)
    {
        lock (_lockObject)
        {
            if (_normalizedCache.TryGetValue(id, out var cached))
            {
                return cached;
            }
        }

        return (string.Empty, null);
    }

    private static IReadOnlyList<MatchSpan> ConvertToMatchSpans(List<int> matchData)
    {
        if (matchData == null || matchData.Count == 0)
        {
            return Array.Empty<MatchSpan>();
        }

        // Convert individual match indices to spans
        var spans = new List<MatchSpan>();
        var sortedIndices = matchData.OrderBy(i => i).ToList();

        int start = sortedIndices[0];
        int length = 1;

        for (int i = 1; i < sortedIndices.Count; i++)
        {
            if (sortedIndices[i] == sortedIndices[i - 1] + 1)
            {
                // Consecutive index, extend the span
                length++;
            }
            else
            {
                // Gap found, save current span and start new one
                spans.Add(new MatchSpan(start, length));
                start = sortedIndices[i];
                length = 1;
            }
        }

        // Add the last span
        spans.Add(new MatchSpan(start, length));

        return spans;
    }

    private static string NormalizeString(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var normalized = input.ToLowerInvariant().Normalize(NormalizationForm.FormKD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
