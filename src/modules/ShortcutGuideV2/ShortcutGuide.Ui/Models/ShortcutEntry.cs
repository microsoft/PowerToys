// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Windows.UI.Text;
using static ShortcutGuide.Models.ShortcutEntry;

namespace ShortcutGuide.Models
{
    public class ShortcutEntry(string name, string? description, bool recommended, ShortcutDescription[] shortcutDescriptions)
    {
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
        }

        public static implicit operator ShortcutTemplateDataObject(ShortcutEntry shortcut)
        {
            List<StackPanel> shortcutStackPanels = [];

            async void AnimateTextBlock(TextBlock animatedTextBlock, string text, int delay = 500)
            {
                int index = 0;

                while (true)
                {
                    animatedTextBlock.Text = text[index].ToString();
                    index = (index + 1) % text.Length;
                    await Task.Delay(delay);
                }
            }

            for (int i = 0; i < shortcut.Shortcut.Length; i++)
            {
                ShortcutDescription shortcutEntry = shortcut.Shortcut[i];
                StackPanel shortcutStackPanel = new();
                shortcutStackPanels.Add(shortcutStackPanel);
                shortcutStackPanel.Orientation = Orientation.Horizontal;

                // If any entry is blank, we skip the whole shortcut
                if (!shortcutEntry.Ctrl && !shortcutEntry.Alt && !shortcutEntry.Shift && !shortcutEntry.Win && shortcutEntry.Keys.Length == 0)
                {
                    return new ShortcutTemplateDataObject(shortcut.Name, shortcut.Description ?? string.Empty, shortcutStackPanel, shortcut);
                }

                if (shortcut.Shortcut.Length > 1)
                {
                    TextBlock shortcutIndexTextBlock = new()
                    {
                        Text = $"{i + 1}.",
                        Margin = new Thickness(3),
                        VerticalAlignment = VerticalAlignment.Center,
                        FontWeight = new FontWeight { Weight = 600 },
                    };
                    shortcutStackPanel.Children.Add(shortcutIndexTextBlock);
                }

                void AddNewTextToStackPanel(string text)
                {
                    shortcutStackPanel.Children.Add(new TextBlock { Text = text, Margin = new Thickness(3), VerticalAlignment = VerticalAlignment.Center });
                }

                if (shortcutEntry.Win)
                {
                    PathIcon winIcon = (XamlReader.Load(@"<PathIcon xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" Data=""M683 1229H0V546h683v683zm819 0H819V546h683v683zm-819 819H0v-683h683v683zm819 0H819v-683h683v683z"" />") as PathIcon)!;
                    Viewbox winIconContainer = new();
                    winIconContainer.Child = winIcon;
                    winIconContainer.HorizontalAlignment = HorizontalAlignment.Center;
                    winIconContainer.VerticalAlignment = VerticalAlignment.Center;
                    winIconContainer.Height = 24;
                    winIconContainer.Width = 24;
                    winIconContainer.Margin = new Thickness(3);
                    shortcutStackPanel.Children.Add(winIconContainer);
                }

                if (shortcutEntry.Ctrl)
                {
                    AddNewTextToStackPanel("Ctrl");
                }

                if (shortcutEntry.Alt)
                {
                    AddNewTextToStackPanel("Alt");
                }

                if (shortcutEntry.Shift)
                {
                    AddNewTextToStackPanel("Shift");
                }

                foreach (object key in shortcutEntry.Keys)
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
                        case "<Underlined letter>":
                            TextBlock animatedTextBlock = new()
                            {
                                Text = "A",
                                Margin = new Thickness(3),
                                VerticalAlignment = VerticalAlignment.Center,

                                // Use monospaced font to ensure the text doesn't move
                                FontFamily = new("Courier New"),
                                TextDecorations = TextDecorations.Underline,
                            };

                            shortcutStackPanel.Children.Add(animatedTextBlock);

                            AnimateTextBlock(animatedTextBlock, "ABCDEFGHIJKLMNOPQRSTUVWXYZ");
                            break;
                        case "<Arrow>":
                            TextBlock arrowTextBlock = new()
                            {
                                Text = "→",
                                Margin = new Thickness(3),
                                VerticalAlignment = VerticalAlignment.Center,
                            };

                            shortcutStackPanel.Children.Add(arrowTextBlock);

                            AnimateTextBlock(arrowTextBlock, "→↓←↑", 1000);
                            break;
                        case "<ArrowLR>":
                            TextBlock arrowLRTextBlock = new()
                            {
                                Text = "→",
                                Margin = new Thickness(3),
                                FontFamily = new("Courier New"),
                                VerticalAlignment = VerticalAlignment.Center,
                            };
                            shortcutStackPanel.Children.Add(arrowLRTextBlock);
                            AnimateTextBlock(arrowLRTextBlock, "→←", 1000);
                            break;
                        case "<ArrowUD>":
                            TextBlock arrowUDTextBlock = new()
                            {
                                Text = "↑",
                                Margin = new Thickness(3),
                                FontFamily = new("Courier New"),
                                VerticalAlignment = VerticalAlignment.Center,
                            };
                            shortcutStackPanel.Children.Add(arrowUDTextBlock);
                            AnimateTextBlock(arrowUDTextBlock, "↑↓", 1000);
                            break;
                        case string name when name.StartsWith('<') && name.EndsWith('>'):
                            AddNewTextToStackPanel(name[1..^1]);
                            break;
                        case int num:
                            if (num == 0)
                            {
                                break;
                            }

                            AddNewTextToStackPanel(Helper.GetKeyName((uint)num));
                            break;
                        case string num when int.TryParse(num, out int parsedNum):
                            if (parsedNum == 0)
                            {
                                break;
                            }

                            AddNewTextToStackPanel(Helper.GetKeyName((uint)parsedNum));
                            break;
                        default:
                            AddNewTextToStackPanel((string)key);
                            break;
                    }
                }
            }

            StackPanel stackPanelToReturn = shortcutStackPanels[0];

            if (shortcutStackPanels.Count == 0)
            {
                return new ShortcutTemplateDataObject(shortcut.Name, shortcut.Description ?? string.Empty, new StackPanel(), shortcut);
            }
            else if (shortcutStackPanels.Count > 1)
            {
                stackPanelToReturn = new StackPanel();

                stackPanelToReturn.Orientation = Orientation.Vertical;
                foreach (StackPanel panel in shortcutStackPanels)
                {
                    panel.Visibility = Visibility.Collapsed;
                    stackPanelToReturn.Children.Add(panel);
                }

                shortcutStackPanels[0].Visibility = Visibility.Visible;
                for (int i = 1; i < shortcutStackPanels.Count; i++)
                {
                    shortcutStackPanels[i].Visibility = Visibility.Collapsed;
                }

                async void AnimateStackPanels(StackPanel[] panels, int delay = 2000)
                {
                    int index = 0;
                    while (true)
                    {
                        foreach (StackPanel panel in panels)
                        {
                            panel.Visibility = Visibility.Collapsed;
                        }

                        panels[index].Visibility = Visibility.Visible;
                        index = (index + 1) % panels.Length;
                        await Task.Delay(delay);
                    }
                }

                AnimateStackPanels([.. shortcutStackPanels]);
            }

            return new ShortcutTemplateDataObject(shortcut.Name, shortcut.Description ?? string.Empty, stackPanelToReturn, shortcut);
        }
    }
}
