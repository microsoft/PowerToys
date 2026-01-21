// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Common.Search;

/// <summary>
/// Describes the capabilities of a search engine.
/// </summary>
public sealed class SearchEngineCapabilities
{
    /// <summary>
    /// Gets a value indicating whether the engine supports fuzzy matching.
    /// </summary>
    public bool SupportsFuzzyMatch { get; init; }

    /// <summary>
    /// Gets a value indicating whether the engine supports semantic search.
    /// </summary>
    public bool SupportsSemanticSearch { get; init; }

    /// <summary>
    /// Gets a value indicating whether the engine persists the index to disk.
    /// </summary>
    public bool PersistsIndex { get; init; }

    /// <summary>
    /// Gets a value indicating whether the engine supports incremental indexing.
    /// </summary>
    public bool SupportsIncrementalIndex { get; init; }

    /// <summary>
    /// Gets a value indicating whether the engine supports match span highlighting.
    /// </summary>
    public bool SupportsMatchSpans { get; init; }
}
