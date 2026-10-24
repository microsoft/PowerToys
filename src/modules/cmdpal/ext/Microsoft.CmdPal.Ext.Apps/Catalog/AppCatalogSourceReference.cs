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

    /// <summary>Gets the shortcut display name, or null for other source representations.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ShortcutName { get; }

    /// <summary>Gets standard shortcut ownership, root-relative depth and source priority, or null for other representations.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AppShortcutPreference? ShortcutPreference { get; }

    /// <summary>Initializes a new instance of the <see cref="AppCatalogSourceReference"/> class. Initializes a source-local application reference.</summary>
    /// <param name="sourceId">The stable source or sub-source identity.</param>
    /// <param name="itemId">The source-local identity or discovery path.</param>
    /// <param name="shortcutName">The display name retained for a shortcut representation, or null for other entries.</param>
    /// <param name="shortcutPreference">The source-provided ownership, depth and priority captured for a Desktop or Start Menu shortcut.</param>
    [JsonConstructor]
    public AppCatalogSourceReference(string sourceId, string itemId, string? shortcutName = null, AppShortcutPreference? shortcutPreference = null)
    {
        ArgumentNullException.ThrowIfNull(sourceId);
        ArgumentNullException.ThrowIfNull(itemId);

        SourceId = sourceId;
        ItemId = itemId;
        ShortcutName = shortcutName;
        ShortcutPreference = shortcutPreference;
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

    /// <summary>Compares retained presentation metadata as well as the source-local identity.</summary>
    internal bool HasSameContent(AppCatalogSourceReference other)
    {
        return Equals(other)
            && string.Equals(ShortcutName, other.ShortcutName, StringComparison.Ordinal)
            && ShortcutPreference == other.ShortcutPreference;
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
