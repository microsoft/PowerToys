// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Common.Search.SemanticSearch;

/// <summary>
/// Options for configuring semantic search queries.
/// </summary>
public class SemanticSearchOptions
{
    /// <summary>
    /// Gets or sets the language for the search query (e.g., "en-US").
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Gets or sets the match scope for the search.
    /// </summary>
    public SemanticSearchMatchScope MatchScope { get; set; } = SemanticSearchMatchScope.Unconstrained;

    /// <summary>
    /// Gets or sets the text match type for lexical matching.
    /// </summary>
    public SemanticSearchTextMatchType TextMatchType { get; set; } = SemanticSearchTextMatchType.Fuzzy;

    /// <summary>
    /// Gets or sets the maximum number of results to return.
    /// </summary>
    public int MaxResults { get; set; } = 10;
}
