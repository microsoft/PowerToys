// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.Windows.Search.AppContentIndex;
using Windows.Graphics.Imaging;
using SearchOperationResult = Common.Search.SearchOperationResult;
using SearchOperationResultT = Common.Search.SearchOperationResult<System.Collections.Generic.IReadOnlyList<Common.Search.SemanticSearch.SemanticSearchResult>>;

namespace Common.Search.SemanticSearch;

/// <summary>
/// A semantic search engine powered by Windows App SDK AI Search APIs.
/// Provides text and image indexing with lexical and semantic search capabilities.
/// </summary>
public sealed class SemanticSearchIndex : IDisposable
{
    private readonly string _indexName;
    private AppContentIndexer? _indexer;
    private bool _disposed;
    private SemanticSearchCapabilities? _capabilities;

    /// <summary>
    /// Initializes a new instance of the <see cref="SemanticSearchIndex"/> class.
    /// </summary>
    /// <param name="indexName">The name of the search index.</param>
    public SemanticSearchIndex(string indexName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
        _indexName = indexName;
    }

    /// <summary>
    /// Gets the last error that occurred during an operation, or null if no error occurred.
    /// </summary>
    public SearchError? LastError { get; private set; }

    /// <summary>
    /// Occurs when the index capabilities change.
    /// </summary>
    public event EventHandler<SemanticSearchCapabilities>? CapabilitiesChanged;

    /// <summary>
    /// Gets a value indicating whether the search engine is initialized.
    /// </summary>
    public bool IsInitialized => _indexer != null;

    /// <summary>
    /// Gets the current index capabilities, or null if not initialized.
    /// </summary>
    public SemanticSearchCapabilities? Capabilities => _capabilities;

    /// <summary>
    /// Initializes the search engine and creates or opens the index.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. Returns a result indicating success or failure with error details.</returns>
    public async Task<SearchOperationResult> InitializeAsync()
    {
        ThrowIfDisposed();
        LastError = null;

        if (_indexer != null)
        {
            Logger.LogDebug($"[SemanticSearchIndex] Already initialized. IndexName={_indexName}");
            return SearchOperationResult.Success();
        }

        Logger.LogInfo($"[SemanticSearchIndex] Initializing. IndexName={_indexName}");

        try
        {
            var result = AppContentIndexer.GetOrCreateIndex(_indexName);
            if (!result.Succeeded)
            {
                var errorDetails = $"Succeeded={result.Succeeded}, ExtendedError={result.ExtendedError}";
                Logger.LogError($"[SemanticSearchIndex] GetOrCreateIndex failed. IndexName={_indexName}, {errorDetails}");
                LastError = SearchError.InitializationFailed(_indexName, errorDetails);
                return SearchOperationResult.Failure(LastError);
            }

            _indexer = result.Indexer;

            // Wait for index capabilities to be ready
            Logger.LogDebug($"[SemanticSearchIndex] Waiting for index capabilities. IndexName={_indexName}");
            await _indexer.WaitForIndexCapabilitiesAsync();

            // Load capabilities
            _capabilities = LoadCapabilities();
            Logger.LogInfo($"[SemanticSearchIndex] Initialized successfully. IndexName={_indexName}, TextLexical={_capabilities.TextLexicalAvailable}, TextSemantic={_capabilities.TextSemanticAvailable}, ImageSemantic={_capabilities.ImageSemanticAvailable}, ImageOcr={_capabilities.ImageOcrAvailable}");

            // Subscribe to capability changes
            _indexer.Listener.IndexCapabilitiesChanged += OnIndexCapabilitiesChanged;

            return SearchOperationResult.Success();
        }
        catch (Exception ex)
        {
            Logger.LogError($"[SemanticSearchIndex] Initialization failed with exception. IndexName={_indexName}", ex);
            LastError = SearchError.InitializationFailed(_indexName, ex.Message, ex);
            return SearchOperationResult.Failure(LastError);
        }
    }

    /// <summary>
    /// Waits for the indexing process to complete.
    /// </summary>
    /// <param name="timeout">The maximum time to wait for indexing to complete.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task WaitForIndexingCompleteAsync(TimeSpan timeout)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        await _indexer!.WaitForIndexingIdleAsync(timeout);
    }

    /// <summary>
    /// Gets the current index capabilities.
    /// </summary>
    /// <returns>The current capabilities of the search index.</returns>
    public SemanticSearchCapabilities GetCapabilities()
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        return _capabilities ?? LoadCapabilities();
    }

    /// <summary>
    /// Adds or updates text content in the index.
    /// </summary>
    /// <param name="id">The unique identifier for the content.</param>
    /// <param name="text">The text content to index.</param>
    /// <returns>A result indicating success or failure with error details.</returns>
    public SearchOperationResult IndexText(string id, string text)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        try
        {
            var content = AppManagedIndexableAppContent.CreateFromString(id, text);
            _indexer!.AddOrUpdate(content);
            return SearchOperationResult.Success();
        }
        catch (Exception ex)
        {
            Logger.LogError($"[SemanticSearchIndex] IndexText failed. Id={id}", ex);
            LastError = SearchError.IndexingFailed(id, ex.Message, ex);
            return SearchOperationResult.Failure(LastError);
        }
    }

    /// <summary>
    /// Adds or updates multiple text contents in the index.
    /// </summary>
    /// <param name="items">A collection of id-text pairs to index.</param>
    /// <returns>A result indicating success or failure with error details. Contains the first error encountered if any.</returns>
    public SearchOperationResult IndexTextBatch(IEnumerable<(string Id, string Text)> items)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();
        ArgumentNullException.ThrowIfNull(items);

        SearchError? firstError = null;

        foreach (var (id, text) in items)
        {
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(text))
            {
                try
                {
                    var content = AppManagedIndexableAppContent.CreateFromString(id, text);
                    _indexer!.AddOrUpdate(content);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[SemanticSearchIndex] IndexTextBatch item failed. Id={id}", ex);
                    firstError ??= SearchError.IndexingFailed(id, ex.Message, ex);
                }
            }
        }

        if (firstError != null)
        {
            LastError = firstError;
            return SearchOperationResult.Failure(firstError);
        }

        return SearchOperationResult.Success();
    }

    /// <summary>
    /// Adds or updates image content in the index.
    /// </summary>
    /// <param name="id">The unique identifier for the image.</param>
    /// <param name="bitmap">The image bitmap to index.</param>
    /// <returns>A result indicating success or failure with error details.</returns>
    public SearchOperationResult IndexImage(string id, SoftwareBitmap bitmap)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(bitmap);

        try
        {
            var content = AppManagedIndexableAppContent.CreateFromBitmap(id, bitmap);
            _indexer!.AddOrUpdate(content);
            return SearchOperationResult.Success();
        }
        catch (Exception ex)
        {
            Logger.LogError($"[SemanticSearchIndex] IndexImage failed. Id={id}", ex);
            LastError = SearchError.IndexingFailed(id, ex.Message, ex);
            return SearchOperationResult.Failure(LastError);
        }
    }

    /// <summary>
    /// Removes content from the index by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the content to remove.</param>
    /// <returns>A result indicating success or failure with error details.</returns>
    public SearchOperationResult Remove(string id)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        try
        {
            _indexer!.Remove(id);
            return SearchOperationResult.Success();
        }
        catch (Exception ex)
        {
            Logger.LogError($"[SemanticSearchIndex] Remove failed. Id={id}", ex);
            LastError = SearchError.Unexpected("Remove", ex);
            return SearchOperationResult.Failure(LastError);
        }
    }

    /// <summary>
    /// Removes all content from the index.
    /// </summary>
    /// <returns>A result indicating success or failure with error details.</returns>
    public SearchOperationResult RemoveAll()
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();

        try
        {
            _indexer!.RemoveAll();
            return SearchOperationResult.Success();
        }
        catch (Exception ex)
        {
            Logger.LogError($"[SemanticSearchIndex] RemoveAll failed.", ex);
            LastError = SearchError.Unexpected("RemoveAll", ex);
            return SearchOperationResult.Failure(LastError);
        }
    }

    /// <summary>
    /// Searches for text content in the index.
    /// </summary>
    /// <param name="searchText">The text to search for.</param>
    /// <param name="options">Optional search options.</param>
    /// <returns>A result containing search results or error details.</returns>
    public SearchOperationResultT SearchText(string searchText, SemanticSearchOptions? options = null)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(searchText);

        options ??= new SemanticSearchOptions();

        try
        {
            var queryOptions = new TextQueryOptions
            {
                MatchScope = ConvertMatchScope(options.MatchScope),
                TextMatchType = ConvertTextMatchType(options.TextMatchType),
            };

            if (!string.IsNullOrEmpty(options.Language))
            {
                queryOptions.Language = options.Language;
            }

            var query = _indexer!.CreateTextQuery(searchText, queryOptions);
            var matches = query.GetNextMatches(options.MaxResults);

            return SearchOperationResultT.Success(ConvertTextMatches(matches));
        }
        catch (Exception ex)
        {
            Logger.LogError($"[SemanticSearchIndex] SearchText failed. Query={searchText}", ex);
            LastError = SearchError.SearchFailed(searchText, ex.Message, ex);
            return SearchOperationResultT.FailureWithFallback(LastError, Array.Empty<SemanticSearchResult>());
        }
    }

    /// <summary>
    /// Searches for image content in the index using text.
    /// </summary>
    /// <param name="searchText">The text to search for in images.</param>
    /// <param name="options">Optional search options.</param>
    /// <returns>A result containing search results or error details.</returns>
    public SearchOperationResultT SearchImages(string searchText, SemanticSearchOptions? options = null)
    {
        ThrowIfDisposed();
        ThrowIfNotInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(searchText);

        options ??= new SemanticSearchOptions();

        try
        {
            var queryOptions = new ImageQueryOptions
            {
                MatchScope = ConvertMatchScope(options.MatchScope),
                ImageOcrTextMatchType = ConvertTextMatchType(options.TextMatchType),
            };

            var query = _indexer!.CreateImageQuery(searchText, queryOptions);
            var matches = query.GetNextMatches(options.MaxResults);

            return SearchOperationResultT.Success(ConvertImageMatches(matches));
        }
        catch (Exception ex)
        {
            Logger.LogError($"[SemanticSearchIndex] SearchImages failed. Query={searchText}", ex);
            LastError = SearchError.SearchFailed(searchText, ex.Message, ex);
            return SearchOperationResultT.FailureWithFallback(LastError, Array.Empty<SemanticSearchResult>());
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_indexer != null)
        {
            _indexer.Listener.IndexCapabilitiesChanged -= OnIndexCapabilitiesChanged;
            _indexer.Dispose();
            _indexer = null;
        }

        _disposed = true;
    }

    private SemanticSearchCapabilities LoadCapabilities()
    {
        var capabilities = _indexer!.GetIndexCapabilities();

        return new SemanticSearchCapabilities
        {
            TextLexicalAvailable = IsCapabilityInitialized(capabilities, IndexCapability.TextLexical),
            TextSemanticAvailable = IsCapabilityInitialized(capabilities, IndexCapability.TextSemantic),
            ImageSemanticAvailable = IsCapabilityInitialized(capabilities, IndexCapability.ImageSemantic),
            ImageOcrAvailable = IsCapabilityInitialized(capabilities, IndexCapability.ImageOcr),
        };
    }

    private static bool IsCapabilityInitialized(IndexCapabilities capabilities, IndexCapability capability)
    {
        var state = capabilities.GetCapabilityState(capability);
        return state.InitializationStatus == IndexCapabilityInitializationStatus.Initialized;
    }

    private void OnIndexCapabilitiesChanged(AppContentIndexer indexer, IndexCapabilities capabilities)
    {
        _capabilities = LoadCapabilities();
        Logger.LogInfo($"[SemanticSearchIndex] Capabilities changed. IndexName={_indexName}, TextLexical={_capabilities.TextLexicalAvailable}, TextSemantic={_capabilities.TextSemanticAvailable}, ImageSemantic={_capabilities.ImageSemanticAvailable}, ImageOcr={_capabilities.ImageOcrAvailable}");
        CapabilitiesChanged?.Invoke(this, _capabilities);
    }

    private static QueryMatchScope ConvertMatchScope(SemanticSearchMatchScope scope)
    {
        return scope switch
        {
            SemanticSearchMatchScope.Unconstrained => QueryMatchScope.Unconstrained,
            SemanticSearchMatchScope.Region => QueryMatchScope.Region,
            SemanticSearchMatchScope.ContentItem => QueryMatchScope.ContentItem,
            _ => QueryMatchScope.Unconstrained,
        };
    }

    private static TextLexicalMatchType ConvertTextMatchType(SemanticSearchTextMatchType matchType)
    {
        return matchType switch
        {
            SemanticSearchTextMatchType.Fuzzy => TextLexicalMatchType.Fuzzy,
            SemanticSearchTextMatchType.Exact => TextLexicalMatchType.Exact,
            _ => TextLexicalMatchType.Fuzzy,
        };
    }

    private static IReadOnlyList<SemanticSearchResult> ConvertTextMatches(IReadOnlyList<TextQueryMatch> matches)
    {
        var results = new List<SemanticSearchResult>();

        foreach (var match in matches)
        {
            var result = new SemanticSearchResult(match.ContentId, SemanticSearchContentKind.Text);

            if (match.ContentKind == QueryMatchContentKind.AppManagedText &&
                match is AppManagedTextQueryMatch textMatch)
            {
                result.TextOffset = textMatch.TextOffset;
                result.TextLength = textMatch.TextLength;
            }

            results.Add(result);
        }

        return results;
    }

    private static IReadOnlyList<SemanticSearchResult> ConvertImageMatches(IReadOnlyList<ImageQueryMatch> matches)
    {
        var results = new List<SemanticSearchResult>();

        foreach (var match in matches)
        {
            var result = new SemanticSearchResult(match.ContentId, SemanticSearchContentKind.Image);
            results.Add(result);
        }

        return results;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void ThrowIfNotInitialized()
    {
        if (_indexer == null)
        {
            throw new InvalidOperationException("Search engine is not initialized. Call InitializeAsync() first.");
        }
    }
}
