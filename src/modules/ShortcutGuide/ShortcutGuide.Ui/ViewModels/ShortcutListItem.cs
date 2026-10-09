// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ShortcutGuide.Models;

namespace ShortcutGuide.ViewModels
{
    public enum ShortcutListItemKind
    {
        Header,
        Subtitle,
        Shortcut,
        EmptyText,
    }

    /// <summary>
    /// Single entry in the flat list rendered by <see cref="Pages.ShortcutsPage"/>.
    /// Flattening lets the page's outer ItemsRepeater virtualize at the row level
    /// (a nested-repeater layout would force every row of every realized section to
    /// be materialized up front).
    /// </summary>
    public sealed class ShortcutListItem
    {
        public ShortcutListItemKind Kind { get; set; }

        public string? Text { get; set; }

        public ShortcutEntry? Shortcut { get; set; }

        public static ShortcutListItem Header(string text) =>
            new() { Kind = ShortcutListItemKind.Header, Text = text };

        public static ShortcutListItem Subtitle(string text) =>
            new() { Kind = ShortcutListItemKind.Subtitle, Text = text };

        public static ShortcutListItem Empty(string text) =>
            new() { Kind = ShortcutListItemKind.EmptyText, Text = text };

        public static ShortcutListItem ForShortcut(ShortcutEntry shortcut) =>
            new() { Kind = ShortcutListItemKind.Shortcut, Shortcut = shortcut };
    }
}
