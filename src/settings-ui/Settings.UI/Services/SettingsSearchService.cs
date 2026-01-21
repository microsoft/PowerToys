// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common.Search;
using Common.Search.FuzzSearch;
using Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.Services
{
    /// <summary>
    /// A service that provides search functionality for settings using pluggable search engines.
    /// </summary>
    public sealed class SettingsSearchService : IDisposable
    {
        private readonly ISearchEngine<SettingEntry> _searchEngine;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsSearchService"/> class with the default FuzzSearchEngine.
        /// </summary>
        public SettingsSearchService()
            : this(new FuzzSearchEngine<SettingEntry>())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsSearchService"/> class with a custom search engine.
        /// </summary>
        /// <param name="searchEngine">The search engine to use.</param>
        public SettingsSearchService(ISearchEngine<SettingEntry> searchEngine)
        {
            ArgumentNullException.ThrowIfNull(searchEngine);
            _searchEngine = searchEngine;
        }

        /// <summary>
        /// Gets a value indicating whether the search service is ready.
        /// </summary>
        public bool IsReady => _searchEngine.IsReady;

        /// <summary>
        /// Gets the search engine capabilities.
        /// </summary>
        public SearchEngineCapabilities Capabilities => _searchEngine.Capabilities;

        /// <summary>
        /// Initializes the search service with the given setting entries.
        /// </summary>
        /// <param name="entries">The setting entries to index.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the async operation.</returns>
        public async Task InitializeAsync(IEnumerable<SettingEntry> entries, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entries);
            ThrowIfDisposed();

            await _searchEngine.InitializeAsync(cancellationToken);
            await _searchEngine.IndexBatchAsync(entries, cancellationToken);
        }

        /// <summary>
        /// Rebuilds the index with new entries.
        /// </summary>
        /// <param name="entries">The new setting entries to index.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the async operation.</returns>
        public async Task RebuildIndexAsync(IEnumerable<SettingEntry> entries, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entries);
            ThrowIfDisposed();

            await _searchEngine.ClearAsync(cancellationToken);
            await _searchEngine.IndexBatchAsync(entries, cancellationToken);
        }

        /// <summary>
        /// Searches for settings matching the query.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of matching setting entries.</returns>
        public async Task<List<SettingEntry>> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(query))
            {
                return [];
            }

            var results = await _searchEngine.SearchAsync(query, cancellationToken: cancellationToken);
            return results.Select(r => r.Item).ToList();
        }

        /// <summary>
        /// Searches for settings matching the query with detailed results.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <param name="options">Search options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of search results with scoring information.</returns>
        public async Task<IReadOnlyList<SearchResult<SettingEntry>>> SearchWithScoresAsync(
            string query,
            SearchOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(query))
            {
                return Array.Empty<SearchResult<SettingEntry>>();
            }

            return await _searchEngine.SearchAsync(query, options, cancellationToken);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _searchEngine.Dispose();
            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }
    }
}
