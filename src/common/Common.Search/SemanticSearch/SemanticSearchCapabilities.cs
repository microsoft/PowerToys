// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Common.Search.SemanticSearch;

/// <summary>
/// Represents the capabilities of the semantic search index.
/// </summary>
public class SemanticSearchCapabilities
{
    /// <summary>
    /// Gets or sets a value indicating whether text lexical (keyword) search is available.
    /// </summary>
    public bool TextLexicalAvailable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether text semantic (AI embedding) search is available.
    /// </summary>
    public bool TextSemanticAvailable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether image semantic search is available.
    /// </summary>
    public bool ImageSemanticAvailable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether image OCR search is available.
    /// </summary>
    public bool ImageOcrAvailable { get; set; }

    /// <summary>
    /// Gets a value indicating whether any search capability is available.
    /// </summary>
    public bool AnyAvailable => TextLexicalAvailable || TextSemanticAvailable || ImageSemanticAvailable || ImageOcrAvailable;

    /// <summary>
    /// Gets a value indicating whether text search (lexical or semantic) is available.
    /// </summary>
    public bool TextSearchAvailable => TextLexicalAvailable || TextSemanticAvailable;

    /// <summary>
    /// Gets a value indicating whether image search (semantic or OCR) is available.
    /// </summary>
    public bool ImageSearchAvailable => ImageSemanticAvailable || ImageOcrAvailable;
}
