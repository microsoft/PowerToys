// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Common.Search;

/// <summary>
/// Defines a pluggable search engine that can index and search items.
/// </summary>
/// <typeparam name="T">The type of items to search, must implement ISearchable.</typeparam>
public interface ISearchEngine<T> : IDisposable
    where T : ISearchable
{
    /// <summary>
    /// Gets a value indicating whether the engine is ready to search.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Gets the engine capabilities.
    /// </summary>
    SearchEngineCapabilities Capabilities { get; }

    /// <summary>
    /// Initializes the search engine.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes a single item.
    /// </summary>
    /// <param name="item">The item to index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    Task IndexAsync(T item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes multiple items in batch.
    /// </summary>
    /// <param name="items">The items to index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    Task IndexBatchAsync(IEnumerable<T> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an item from the index by its ID.
    /// </summary>
    /// <param name="id">The ID of the item to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    Task RemoveAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all indexed items.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for items matching the query.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="options">Optional search options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of search results ordered by relevance.</returns>
    Task<IReadOnlyList<SearchResult<T>>> SearchAsync(
        string query,
        SearchOptions? options = null,
        CancellationToken cancellationToken = default);
}
