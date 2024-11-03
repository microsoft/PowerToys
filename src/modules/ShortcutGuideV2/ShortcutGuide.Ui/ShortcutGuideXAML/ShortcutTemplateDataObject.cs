// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ShortcutGuide.Models;

namespace ShortcutGuide
{
    public sealed class ShortcutTemplateDataObject
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public StackPanel Shortcut { get; set; }

        public Shortcut OriginalShortcutObject { get; set; }

        public Visibility DescriptionVisible { get; set; }

        public ShortcutTemplateDataObject(string name, string description, StackPanel shortcut, Shortcut originalShortcutObject)
        {
            Name = name;
            Description = description;
            OriginalShortcutObject = originalShortcutObject;

            DescriptionVisible = string.IsNullOrWhiteSpace(description) ? Visibility.Collapsed : Visibility.Visible;

            shortcut.Orientation = Orientation.Horizontal;
            Shortcut = shortcut;
        }
    }
}
