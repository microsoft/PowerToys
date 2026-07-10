// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace ShortcutGuide.Models
{
    public class ShortcutEntry(string name, string? description, bool recommended, ShortcutDescription[] shortcutDescriptions)
    {
        public override bool Equals(object? obj)
        {
            return obj is ShortcutEntry other && this.Name == other.Name &&
                   this.Description == other.Description &&
                   this.Shortcut.Length == other.Shortcut.Length &&
                   this.Shortcut.SequenceEqual(other.Shortcut);
        }

        public override int GetHashCode()
        {
            var hash = HashCode.Combine(Name, Description);
            foreach (var s in Shortcut)
            {
                hash = HashCode.Combine(hash, s.GetHashCode());
            }

            return hash;
        }

        public static bool operator ==(ShortcutEntry? left, ShortcutEntry? right)
        {
            return (left is null && right is null) || (left is not null && right is not null && left.Equals(right));
        }

        public static bool operator !=(ShortcutEntry? left, ShortcutEntry? right)
        {
            return !(left == right);
        }

        public ShortcutEntry()
            : this(string.Empty, string.Empty, false, [])
        {
        }

        [JsonPropertyName(nameof(Name))]
        public string Name { get; set; } = name;

        [JsonPropertyName(nameof(Description))]
        public string? Description { get; set; } = description;

        [JsonPropertyName(nameof(Recommended))]
        public bool Recommended { get; set; } = recommended;

        [JsonPropertyName(nameof(Shortcut))]
        public ShortcutDescription[] Shortcut { get; set; } = shortcutDescriptions;
    }
}
