// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.Services.Search
{
    /// <summary>
    /// Defines a pluggable search provider interface for settings search.
    /// </summary>
    public interface ISearchProvider
    {
        /// <summary>
        /// Gets a value indicating whether the provider is ready to search.
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Initializes the search provider with the given index entries.
        /// </summary>
        /// <param name="entries">The setting entries to index.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the async operation.</returns>
        Task InitializeAsync(IReadOnlyList<SettingEntry> entries, CancellationToken cancellationToken = default);

        /// <summary>
        /// Searches for settings matching the query.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of matching setting entries ordered by relevance.</returns>
        List<SettingEntry> Search(string query, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears the index and releases resources.
        /// </summary>
        void Clear();
    }
}
