// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Windows.UI.Text;
using static ShortcutGuide.Models.ShortcutEntry;
using Orientation = Microsoft.UI.Xaml.Controls.Orientation;

namespace ShortcutGuide.Models
{
    public class ShortcutEntry(string name, string? description, bool recommended, ShortcutDescription[] shortcutDescriptions)
    {
        public override bool Equals(object? obj)
        {
            return obj is ShortcutEntry other && Name == other.Name &&
                   Description == other.Description &&
                   Shortcut.Length == other.Shortcut.Length &&
                   Shortcut.SequenceEqual(other.Shortcut);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
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

        public class ShortcutDescription(bool ctrl, bool shift, bool alt, bool win, string[] keys)
        {
            public ShortcutDescription()
                : this(false, false, false, false, [])
            {
            }

            [JsonPropertyName(nameof(Ctrl))]
            public bool Ctrl { get; set; } = ctrl;

            [JsonPropertyName(nameof(Shift))]
            public bool Shift { get; set; } = shift;

            [JsonPropertyName(nameof(Alt))]
            public bool Alt { get; set; } = alt;

            [JsonPropertyName(nameof(Win))]
            public bool Win { get; set; } = win;

            [JsonPropertyName(nameof(Keys))]
            public string[] Keys { get; set; } = keys;

            public override bool Equals(object? obj)
            {
                return obj is ShortcutDescription other && Ctrl == other.Ctrl &&
                       Shift == other.Shift &&
                       Alt == other.Alt &&
                       Win == other.Win &&
                       Keys.SequenceEqual(other.Keys);
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }

            public static bool operator ==(ShortcutDescription? left, ShortcutDescription? right)
            {
                return (left is null && right is null) || (left is not null && right is not null && left.Equals(right));
            }

            public static bool operator !=(ShortcutDescription? left, ShortcutDescription? right)
            {
                return !(left == right);
            }
        }
    }
}
