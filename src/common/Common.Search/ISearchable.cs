// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Common.Search;

/// <summary>
/// Defines a searchable item that can be indexed and searched.
/// </summary>
public interface ISearchable
{
    /// <summary>
    /// Gets the unique identifier for this item.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the primary searchable text (e.g., title, header).
    /// </summary>
    string SearchableText { get; }

    /// <summary>
    /// Gets optional secondary searchable text (e.g., description).
    /// Returns null if not available.
    /// </summary>
    string? SecondarySearchableText { get; }
}
