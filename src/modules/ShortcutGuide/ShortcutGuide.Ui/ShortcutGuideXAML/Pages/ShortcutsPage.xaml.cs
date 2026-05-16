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
    /// Displays every category of shortcuts for the selected application as a single,
    /// flat virtualized list (Pinned, Recommended, each category, Taskbar).
    /// The list is flat — header / subtitle / shortcut / empty-placeholder rows share one
    /// ItemsRepeater — so virtualization realizes only rows in the viewport instead of
    /// every row of every realized section.
    /// </summary>
    public sealed partial class ShortcutsPage : Page
    {
        private const string TaskbarSectionMarker = "<TASKBAR1-9>";

        private ShortcutFile? _shortcutFile;
        private string _appName = string.Empty;

        public ObservableCollection<ShortcutListItem> Rows { get; } = new();

        public ShortcutsPage()
        {
            this.InitializeComponent();
            this.Unloaded += (_, _) => PinnedShortcutsHelper.PinnedShortcutsChanged -= this.OnPinnedShortcutsChanged;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is ShortcutPageNavParam param)
            {
                this._appName = param.AppName;
                this._shortcutFile = param.ShortcutFile;
                this.RebuildRows();
            }

            PinnedShortcutsHelper.PinnedShortcutsChanged -= this.OnPinnedShortcutsChanged;
            PinnedShortcutsHelper.PinnedShortcutsChanged += this.OnPinnedShortcutsChanged;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            PinnedShortcutsHelper.PinnedShortcutsChanged -= this.OnPinnedShortcutsChanged;
        }

        private void RebuildRows()
        {
            this.Rows.Clear();

            if (this._shortcutFile is not { } file)
            {
                return;
            }

            // 1. Pinned (always shown, with empty-state placeholder).
            this.Rows.Add(ShortcutListItem.Header(
                ResourceLoaderInstance.ResourceLoader.GetString("PinnedHeaderTxt/Text")));
            var pinned = App.PinnedShortcuts.TryGetValue(this._appName, out var pinnedItems)
                ? (IReadOnlyList<ShortcutEntry>)pinnedItems
                : Array.Empty<ShortcutEntry>();
            if (pinned.Count == 0)
            {
                this.Rows.Add(ShortcutListItem.Empty(
                    ResourceLoaderInstance.ResourceLoader.GetString("PinnedEmptyText/Text")));
            }
            else
            {
                foreach (var s in pinned)
                {
                    this.Rows.Add(ShortcutListItem.ForShortcut(s));
                }
            }

            // 2. Recommended (only if non-empty).
            var recommended = file.Shortcuts?
                .SelectMany(c => c.Properties ?? Array.Empty<ShortcutEntry>())
                .Where(s => s.Recommended)
                .ToList() ?? new List<ShortcutEntry>();
            if (recommended.Count > 0)
            {
                this.Rows.Add(ShortcutListItem.Header(
                    ResourceLoaderInstance.ResourceLoader.GetString("RecommendedHeaderText/Text")));
                foreach (var s in recommended)
                {
                    this.Rows.Add(ShortcutListItem.ForShortcut(s));
                }
            }

            // 3. One section per real category (skip <...> meta sections).
            ShortcutCategory? taskbarCategory = null;
            if (file.Shortcuts is not null)
            {
                foreach (var category in file.Shortcuts)
                {
                    string name = category.SectionName ?? string.Empty;

                    // Taskbar marker may carry trailing text in the manifest (e.g. "<TASKBAR1-9>Taskbar Shortcuts");
                    // detect it by prefix and hand it off to the dedicated Taskbar section below.
                    if (name.StartsWith(TaskbarSectionMarker, StringComparison.Ordinal))
                    {
                        taskbarCategory = category;
                        continue;
                    }

                    if (name.StartsWith('<') && name.EndsWith('>'))
                    {
                        continue;
                    }

                    var items = category.Properties ?? Array.Empty<ShortcutEntry>();
                    if (items.Length == 0)
                    {
                        continue;
                    }

                    this.Rows.Add(ShortcutListItem.Header(name));
                    foreach (var s in items)
                    {
                        this.Rows.Add(ShortcutListItem.ForShortcut(s));
                    }
                }
            }

            // 4. Taskbar (Windows only).
            if (taskbarCategory is { } tb && tb.Properties is { Length: > 0 } taskbarItems)
            {
                this.Rows.Add(ShortcutListItem.Header(
                    ResourceLoaderInstance.ResourceLoader.GetString("TaskbarHeaderTxt/Text")));
                this.Rows.Add(ShortcutListItem.Subtitle(
                    ResourceLoaderInstance.ResourceLoader.GetString("TaskbarDescriptionTxt/Text")));
                foreach (var s in taskbarItems)
                {
                    this.Rows.Add(ShortcutListItem.ForShortcut(s));
                }
            }
        }

        private void OnPinnedShortcutsChanged(object? sender, string appName)
        {
            if (appName == this._appName)
            {
                this.RebuildRows();
            }
        }
    }
}
