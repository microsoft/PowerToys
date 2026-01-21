// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Common.Search;

/// <summary>
/// Represents a search result with the matched item and scoring information.
/// </summary>
/// <typeparam name="T">The type of the matched item.</typeparam>
public sealed class SearchResult<T>
    where T : ISearchable
{
    /// <summary>
    /// Gets the matched item.
    /// </summary>
    public required T Item { get; init; }

    /// <summary>
    /// Gets the relevance score (higher is more relevant).
    /// </summary>
    public required double Score { get; init; }

    /// <summary>
    /// Gets the type of match that produced this result.
    /// </summary>
    public required SearchMatchKind MatchKind { get; init; }

    /// <summary>
    /// Gets the match details for highlighting (optional).
    /// </summary>
    public IReadOnlyList<MatchSpan>? MatchSpans { get; init; }
}
