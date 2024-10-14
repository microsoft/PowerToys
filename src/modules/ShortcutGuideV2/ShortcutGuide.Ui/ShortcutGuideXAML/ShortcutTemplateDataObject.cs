// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ShortcutGuide
{
    public sealed class ShortcutTemplateDataObject
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public StackPanel Shortcut { get; set; }

        public Visibility DescriptionVisible { get; set; }

        public ShortcutTemplateDataObject(string name, string description, StackPanel shortcut)
        {
            Name = name;
            Description = description;

            if (string.IsNullOrWhiteSpace(description))
            {
                DescriptionVisible = Visibility.Collapsed;
            }
            else
            {
                DescriptionVisible = Visibility.Visible;
            }

            shortcut.Orientation = Orientation.Horizontal;
            Shortcut = shortcut;
        }
    }
}
