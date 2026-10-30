// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>Captures standard-shortcut tie-breaks at equal representation priority.</summary>
/// <remarks>The catalog compares <see cref="AppCatalogProvenance.Priority"/> before these preferences.</remarks>
/// <param name="IsUser">Whether the shortcut belongs to the current user's root rather than the shared root.</param>
/// <param name="DirectoryDepth">The number of directories below Desktop or Start Menu\Programs.</param>
/// <param name="SourcePriority">The discovery origin's priority; lower values win after ownership and depth are equal.</param>
internal readonly record struct AppShortcutPreference(bool IsUser, int DirectoryDepth, int SourcePriority) : IComparable<AppShortcutPreference>
{
    /// <summary>Prefers personal shortcuts, then shallower shortcuts, then source priority.</summary>
    public int CompareTo(AppShortcutPreference other)
    {
        var comparison = other.IsUser.CompareTo(IsUser);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = DirectoryDepth.CompareTo(other.DirectoryDepth);
        return comparison != 0 ? comparison : SourcePriority.CompareTo(other.SourcePriority);
    }
}
