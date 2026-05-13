// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Common.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ShortcutGuide.Helpers;
using ShortcutGuide.Models;
using ShortcutGuide.ViewModels;

namespace ShortcutGuide.Pages
{
    /// <summary>
    /// Displays every category of shortcuts for the selected application as a single
    /// scrolling list, with section headers (Pinned, Recommended, each category, Taskbar).
    /// All UI is declared in XAML; this code-behind only assembles the section view-models.
    /// </summary>
    public sealed partial class ShortcutsPage : Page
    {
        private const string TaskbarSectionMarker = "<TASKBAR1-9>";

        private readonly ShortcutSection _pinnedSection;

        private string _appName = string.Empty;

        public ObservableCollection<ShortcutSection> Sections { get; } = new();

        public ShortcutsPage()
        {
            this._pinnedSection = new ShortcutSection
            {
                Title = ResourceLoaderInstance.ResourceLoader.GetString("PinnedHeaderTxt/Text"),
                EmptyText = ResourceLoaderInstance.ResourceLoader.GetString("PinnedEmptyText/Text"),
            };

            this.InitializeComponent();
            this.Unloaded += (_, _) => PinnedShortcutsHelper.PinnedShortcutsChanged -= this.OnPinnedShortcutsChanged;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is ShortcutPageNavParam param)
            {
                this._appName = param.AppName;
                this.BuildSections(param.ShortcutFile);
            }

            PinnedShortcutsHelper.PinnedShortcutsChanged -= this.OnPinnedShortcutsChanged;
            PinnedShortcutsHelper.PinnedShortcutsChanged += this.OnPinnedShortcutsChanged;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            PinnedShortcutsHelper.PinnedShortcutsChanged -= this.OnPinnedShortcutsChanged;
        }

        private void BuildSections(ShortcutFile shortcutFile)
        {
            this.Sections.Clear();

            // 1. Pinned (always present, at the top).
            this.RefreshPinnedItems();
            this.Sections.Add(this._pinnedSection);

            // 2. Recommended across all categories.
            var recommended = shortcutFile.Shortcuts?
                .SelectMany(c => c.Properties ?? Array.Empty<ShortcutEntry>())
                .Where(s => s.Recommended)
                .ToList() ?? new List<ShortcutEntry>();

            if (recommended.Count > 0)
            {
                this.Sections.Add(BuildSection(
                    ResourceLoaderInstance.ResourceLoader.GetString("RecommendedHeaderText/Text"),
                    recommended));
            }

            // 3. One section per real category (skip <...> meta sections).
            ShortcutCategory? taskbarCategory = null;
            if (shortcutFile.Shortcuts is not null)
            {
                foreach (var category in shortcutFile.Shortcuts)
                {
                    string name = category.SectionName ?? string.Empty;
                    if (name.StartsWith('<') && name.EndsWith('>'))
                    {
                        if (name == TaskbarSectionMarker)
                        {
                            taskbarCategory = category;
                        }

                        continue;
                    }

                    var items = category.Properties ?? Array.Empty<ShortcutEntry>();
                    if (items.Length == 0)
                    {
                        continue;
                    }

                    this.Sections.Add(BuildSection(name, items));
                }
            }

            // 4. Taskbar (Windows only).
            if (taskbarCategory is { } tb && tb.Properties is { Length: > 0 } taskbarItems)
            {
                this.Sections.Add(BuildSection(
                    ResourceLoaderInstance.ResourceLoader.GetString("TaskbarHeaderTxt/Text"),
                    taskbarItems,
                    ResourceLoaderInstance.ResourceLoader.GetString("TaskbarDescriptionTxt/Text")));
            }
        }

        private static ShortcutSection BuildSection(string title, IEnumerable<ShortcutEntry> items, string? subtitle = null)
        {
            var section = new ShortcutSection { Title = title, Subtitle = subtitle };
            foreach (var item in items)
            {
                section.Items.Add(item);
            }

            return section;
        }

        private void RefreshPinnedItems()
        {
            this._pinnedSection.Items.Clear();
            if (App.PinnedShortcuts.TryGetValue(this._appName, out var current))
            {
                foreach (var s in current)
                {
                    this._pinnedSection.Items.Add(s);
                }
            }
        }

        private void OnPinnedShortcutsChanged(object? sender, string appName)
        {
            if (appName == this._appName)
            {
                this.RefreshPinnedItems();
            }
        }
    }
}
