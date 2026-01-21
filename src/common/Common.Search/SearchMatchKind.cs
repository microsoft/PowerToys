// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Common.Search;

/// <summary>
/// Specifies the kind of match that produced a search result.
/// </summary>
public enum SearchMatchKind
{
    /// <summary>
    /// Exact text match.
    /// </summary>
    Exact,

    /// <summary>
    /// Fuzzy/approximate text match.
    /// </summary>
    Fuzzy,

    /// <summary>
    /// Semantic/AI-based match.
    /// </summary>
    Semantic,

    /// <summary>
    /// Combined match from multiple engines.
    /// </summary>
    Composite,
}
