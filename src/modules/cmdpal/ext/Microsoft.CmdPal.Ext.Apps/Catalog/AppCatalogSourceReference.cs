// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>
/// Identifies one source representation that contributed to a canonical application.
/// </summary>
internal sealed class AppCatalogSourceReference : IEquatable<AppCatalogSourceReference>, IComparable<AppCatalogSourceReference>
{
    /// <summary>Gets the stable source or sub-source identity.</summary>
    public string SourceId { get; }

    /// <summary>Gets the source-local identity, usually a normalized discovery path.</summary>
    public string ItemId { get; }

    /// <summary>Initializes a new instance of the <see cref="AppCatalogSourceReference"/> class. Initializes a source-local application reference.</summary>
    [JsonConstructor]
    public AppCatalogSourceReference(string sourceId, string itemId)
    {
        ArgumentNullException.ThrowIfNull(sourceId);
        ArgumentNullException.ThrowIfNull(itemId);

        SourceId = sourceId;
        ItemId = itemId;
    }

    /// <inheritdoc />
    public bool Equals(AppCatalogSourceReference? other)
    {
        return other is not null
            && string.Equals(SourceId, other.SourceId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(ItemId, other.ItemId, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as AppCatalogSourceReference);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(SourceId),
            StringComparer.OrdinalIgnoreCase.GetHashCode(ItemId));
    }

    /// <summary>
    /// Compares references deterministically, treating case-only differences as the same logical reference.
    /// </summary>
    public int CompareTo(AppCatalogSourceReference? other)
    {
        if (other is null)
        {
            return 1;
        }

        var comparison = string.Compare(SourceId, other.SourceId, StringComparison.OrdinalIgnoreCase);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = string.Compare(ItemId, other.ItemId, StringComparison.OrdinalIgnoreCase);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = string.Compare(SourceId, other.SourceId, StringComparison.Ordinal);
        return comparison != 0
            ? comparison
            : string.Compare(ItemId, other.ItemId, StringComparison.Ordinal);
    }
}
