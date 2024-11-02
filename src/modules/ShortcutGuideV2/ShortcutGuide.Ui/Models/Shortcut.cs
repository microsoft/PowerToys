// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Windows.Security.Cryptography.Certificates;
using YamlDotNet.Core.Tokens;

namespace ShortcutGuide.Models
{
    public struct Shortcut(string name, string? description, bool recommended, bool ctrl, bool shift, bool alt, bool win, string[] keys)
    {
        public string Name = name;
        public string? Description = description;
        public bool Recommended = recommended;

        public bool Ctrl = ctrl;
        public bool Shift = shift;
        public bool Alt = alt;
        public bool Win = win;
        public string[] Keys = keys;

        public static implicit operator ShortcutTemplateDataObject(Shortcut shortcut)
        {
            StackPanel shortcutStackPanel = new();

            shortcutStackPanel.Orientation = Orientation.Horizontal;

            if (shortcut.Ctrl == false && shortcut.Alt == false && shortcut.Shift == false && shortcut.Win == false && shortcut.Keys.Length == 0)
            {
                return new ShortcutTemplateDataObject(shortcut.Name, shortcut.Description ?? string.Empty, shortcutStackPanel);
            }

            void AddNewTextToStackPanel(string text)
            {
                shortcutStackPanel.Children.Add(new TextBlock { Text = text, Margin = new Thickness(3), VerticalAlignment = VerticalAlignment.Center });
            }

            if (shortcut.Win)
            {
                PathIcon winIcon = (XamlReader.Load(@"<PathIcon xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" Data=""M683 1229H0V546h683v683zm819 0H819V546h683v683zm-819 819H0v-683h683v683zm819 0H819v-683h683v683z"" />") as PathIcon)!;
                Viewbox winIconContainer = new Viewbox();
                winIconContainer.Child = winIcon;
                winIconContainer.HorizontalAlignment = HorizontalAlignment.Center;
                winIconContainer.VerticalAlignment = VerticalAlignment.Center;
                winIconContainer.Height = 24;
                winIconContainer.Width = 24;
                winIconContainer.Margin = new Thickness(3);
                shortcutStackPanel.Children.Add(winIconContainer);
            }

            if (shortcut.Ctrl)
            {
                AddNewTextToStackPanel("Ctrl");
            }

            if (shortcut.Alt)
            {
                AddNewTextToStackPanel("Alt");
            }

            if (shortcut.Shift)
            {
                AddNewTextToStackPanel("Shift");
            }

            foreach (string key in shortcut.Keys)
            {
                switch (key)
                {
                    case "<Copilot>":
                        shortcutStackPanel.Children.Add(new BitmapIcon() { UriSource = new("ms-appx:///Assets/ShortcutGuide/CopilotKey.png") });
                        break;
                    case "<Office>":
                        shortcutStackPanel.Children.Add(new BitmapIcon() { UriSource = new("ms-appx:///Assets/ShortcutGuide/OfficeKey.png"), Height = 20, Width = 20 });
                        break;
                    case "<Left>":
                        AddNewTextToStackPanel("←");
                        break;
                    case "<Right>":
                        AddNewTextToStackPanel("→");
                        break;
                    case "<Up>":
                        AddNewTextToStackPanel("↑");
                        break;
                    case "<Down>":
                        AddNewTextToStackPanel("↓");
                        break;
                    case string name when name.StartsWith('<'):
                        AddNewTextToStackPanel(key.Replace("<", string.Empty).Replace(">", string.Empty));
                        break;
                    default:
                        AddNewTextToStackPanel(key);
                        break;
                }
            }

            return new ShortcutTemplateDataObject(shortcut.Name, shortcut.Description ?? string.Empty, shortcutStackPanel);
        }
    }
}
