// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Common.Search.SemanticSearch;

/// <summary>
/// Represents a search result from the semantic search engine.
/// </summary>
public class SemanticSearchResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SemanticSearchResult"/> class.
    /// </summary>
    /// <param name="contentId">The unique identifier of the matched content.</param>
    /// <param name="contentKind">The kind of content matched (text or image).</param>
    public SemanticSearchResult(string contentId, SemanticSearchContentKind contentKind)
    {
        ContentId = contentId;
        ContentKind = contentKind;
    }

    /// <summary>
    /// Gets the unique identifier of the matched content.
    /// </summary>
    public string ContentId { get; }

    /// <summary>
    /// Gets the kind of content that was matched.
    /// </summary>
    public SemanticSearchContentKind ContentKind { get; }

    /// <summary>
    /// Gets or sets the text offset where the match was found (for text matches only).
    /// </summary>
    public int TextOffset { get; set; }

    /// <summary>
    /// Gets or sets the length of the matched text (for text matches only).
    /// </summary>
    public int TextLength { get; set; }
}
