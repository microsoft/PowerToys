// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Common.Search.SemanticSearch;

/// <summary>
/// Specifies the type of text matching for lexical searches.
/// </summary>
public enum SemanticSearchTextMatchType
{
    /// <summary>
    /// Fuzzy matching allows spelling errors and approximate words.
    /// </summary>
    Fuzzy,

    /// <summary>
    /// Exact matching requires exact text matches.
    /// </summary>
    Exact,
}
