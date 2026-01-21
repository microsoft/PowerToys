// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Common.Search.SemanticSearch;

/// <summary>
/// Specifies the scope for semantic search matching.
/// </summary>
public enum SemanticSearchMatchScope
{
    /// <summary>
    /// No constraints, uses both Lexical and Semantic matching.
    /// </summary>
    Unconstrained,

    /// <summary>
    /// Restrict matching to a specific region.
    /// </summary>
    Region,

    /// <summary>
    /// Restrict matching to a single content item.
    /// </summary>
    ContentItem,
}
