// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ShortcutGuide.Controls;
using ShortcutGuide.Helpers;
using ShortcutGuide.Models;
using ShortcutGuide.ViewModels;

namespace ShortcutGuide.Pages
{
    public sealed partial class ShortcutsPage : Page
    {
        private const string TaskbarSectionMarker = "<TASKBAR1-9>";

        private ShortcutFile? _shortcutFile;
        private string _appName = string.Empty;
        private string _searchQuery = string.Empty;
        private bool _isEventSubscribed;

        public ObservableCollection<ShortcutListItem> Rows { get; } = new();

        public ShortcutsPage()
        {
            this.InitializeComponent();

            this.Unloaded += (_, _) =>
            {
                UnsubscribeFromEvents();
                ClearData();
            };
        }

        private void MainItemsRepeater_ElementClearing(ItemsRepeater sender, ItemsRepeaterElementClearingEventArgs args)
        {
            // Aggressively clean up elements as they're being cleared
            if (args.Element is FrameworkElement element)
            {
                // Clear DataContext to break binding references
                element.DataContext = null;
                if (element is ShortcutItemView shortcutView)
                {
                    shortcutView.ClearValue(ShortcutItemView.ShortcutProperty);
                }
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            UnsubscribeFromEvents();
            PinnedShortcutsHelper.PinnedShortcutsChanged += this.OnPinnedShortcutsChanged;
            _isEventSubscribed = true;
        }

        /// <summary>
        /// Refreshes the page to show the shortcuts for the given app. The page
        /// (and its <c>ItemsRepeater</c>) is reused across opens instead of
        /// being recreated, so only the <see cref="Rows"/> collection changes.
        /// </summary>
        public void SetShortcuts(ShortcutFile file, string appName, string searchQuery)
        {
            this._appName = appName;
            this._shortcutFile = file;
            this._searchQuery = searchQuery;
            this.RebuildRows();
        }

        public void SetSearchQuery(string searchQuery)
        {
            if (string.Equals(this._searchQuery, searchQuery, StringComparison.Ordinal))
            {
                return;
            }

            this._searchQuery = searchQuery;
            this.RebuildRows();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            UnsubscribeFromEvents();
            ClearData();
        }

        public void ClearData()
        {
            // Clear the collection to trigger ElementClearing for all items
            this.Rows.Clear();
            this.UpdateNoResultsState(false);
            _shortcutFile = null;
            _appName = string.Empty;
            _searchQuery = string.Empty;
        }

        private void UnsubscribeFromEvents()
        {
            if (_isEventSubscribed)
            {
                PinnedShortcutsHelper.PinnedShortcutsChanged -= this.OnPinnedShortcutsChanged;
                _isEventSubscribed = false;
            }
        }

        private void RebuildRows()
        {
            this.Rows.Clear();

            if (this._shortcutFile is not { } file)
            {
                this.UpdateNoResultsState(false);
                return;
            }

            string normalizedQuery = this._searchQuery.Trim();
            bool isSearchActive = normalizedQuery.Length > 0;
            bool hasMatches = false;

            // 1. Pinned (always shown with an empty-state placeholder when not searching).
            var pinned = App.PinnedShortcuts.TryGetValue(this._appName, out var pinnedItems)
                ? (IReadOnlyList<ShortcutEntry>)pinnedItems
                : Array.Empty<ShortcutEntry>();
            var filteredPinned = FilterShortcuts(pinned, normalizedQuery);
            if (filteredPinned.Count > 0 || !isSearchActive)
            {
                this.Rows.Add(ShortcutListItem.Header(
                    ResourceLoaderInstance.ResourceLoader.GetString("PinnedHeaderTxt/Text")));
                if (filteredPinned.Count == 0)
                {
                    this.Rows.Add(ShortcutListItem.Empty(
                        ResourceLoaderInstance.ResourceLoader.GetString("PinnedEmptyText/Text")));
                }
                else
                {
                    AddShortcuts(filteredPinned);
                }
            }

            // 2. Recommended (only if matching shortcuts are present).
            var recommended = file.Shortcuts?
                .SelectMany(c => c.Properties ?? Array.Empty<ShortcutEntry>())
                .Where(s => s.Recommended)
                .ToList() ?? new List<ShortcutEntry>();
            var filteredRecommended = FilterShortcuts(recommended, normalizedQuery);
            if (filteredRecommended.Count > 0)
            {
                this.Rows.Add(ShortcutListItem.Header(
                    ResourceLoaderInstance.ResourceLoader.GetString("RecommendedHeaderText/Text")));
                AddShortcuts(filteredRecommended);
            }

            void AddShortcuts(IReadOnlyList<ShortcutEntry> shortcuts)
            {
                foreach (var shortcut in shortcuts)
                {
                    this.Rows.Add(ShortcutListItem.ForShortcut(shortcut));
                    hasMatches = true;
                }
            }

            // 3. One section per real category (skip <...> meta sections).
            ShortcutCategory? taskbarCategory = null;
            if (file.Shortcuts is not null)
            {
                foreach (var category in file.Shortcuts)
                {
                    string name = category.SectionName ?? string.Empty;

                    if (name.StartsWith(TaskbarSectionMarker, StringComparison.Ordinal))
                    {
                        taskbarCategory = category;
                        continue;
                    }

                    if (name.StartsWith('<') && name.EndsWith('>'))
                    {
                        continue;
                    }

                    var items = FilterShortcuts(category.Properties ?? Array.Empty<ShortcutEntry>(), normalizedQuery);
                    if (items.Count == 0)
                    {
                        continue;
                    }

                    this.Rows.Add(ShortcutListItem.Header(name));
                    AddShortcuts(items);
                }
            }

            // 4. Taskbar (Windows only).
            if (taskbarCategory is { } tb && tb.Properties is { Length: > 0 } taskbarItems)
            {
                var filteredTaskbarItems = FilterShortcuts(taskbarItems, normalizedQuery);
                if (filteredTaskbarItems.Count > 0)
                {
                    this.Rows.Add(ShortcutListItem.Header(
                        ResourceLoaderInstance.ResourceLoader.GetString("TaskbarHeaderTxt/Text")));
                    this.Rows.Add(ShortcutListItem.Subtitle(
                        ResourceLoaderInstance.ResourceLoader.GetString("TaskbarDescriptionTxt/Text")));
                    AddShortcuts(filteredTaskbarItems);
                }
            }

            this.UpdateNoResultsState(isSearchActive && !hasMatches);
        }

        private void UpdateNoResultsState(bool isVisible)
        {
            if (isVisible)
            {
                if (this.NoResultsTextBlock.Visibility == Visibility.Visible)
                {
                    return;
                }

                this.NoResultsTextBlock.Visibility = Visibility.Visible;
                this.NoResultsTextBlock.Text = ResourceLoaderInstance.ResourceLoader.GetString("SearchBlank");
                var peer = FrameworkElementAutomationPeer.FromElement(this.NoResultsTextBlock)
                    ?? FrameworkElementAutomationPeer.CreatePeerForElement(this.NoResultsTextBlock);
                if (peer is not null && AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
                {
                    peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
                }
            }
            else
            {
                this.NoResultsTextBlock.Visibility = Visibility.Collapsed;
                this.NoResultsTextBlock.Text = string.Empty;
            }
        }

        private static IReadOnlyList<ShortcutEntry> FilterShortcuts(IReadOnlyList<ShortcutEntry> shortcuts, string query)
        {
            if (query.Length == 0)
            {
                return shortcuts;
            }

            return shortcuts
                .Where(shortcut => ShortcutSearchMatcher.Matches(shortcut, query))
                .ToArray();
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
