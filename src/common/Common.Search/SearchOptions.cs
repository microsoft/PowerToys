// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Common.Search;

/// <summary>
/// Options for configuring search behavior.
/// </summary>
public sealed class SearchOptions
{
    /// <summary>
    /// Gets or sets the maximum number of results to return.
    /// Default is 20.
    /// </summary>
    public int MaxResults { get; set; } = 20;

    /// <summary>
    /// Gets or sets the minimum score threshold.
    /// Results below this score are filtered out.
    /// Default is 0.0 (no filtering).
    /// </summary>
    public double MinScore { get; set; }

    /// <summary>
    /// Gets or sets the language hint for the search (e.g., "en-US").
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to include match spans for highlighting.
    /// Default is false.
    /// </summary>
    public bool IncludeMatchSpans { get; set; }
}
