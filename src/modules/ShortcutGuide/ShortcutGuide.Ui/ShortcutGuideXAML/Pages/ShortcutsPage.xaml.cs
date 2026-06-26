// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
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
        private bool _isEventSubscribed;

        public ObservableCollection<ShortcutListItem> Rows { get; } = new();

        public ShortcutsPage()
        {
            this.InitializeComponent();

            this.Unloaded += (_, _) =>
            {
                UnsubscribeFromEvents();
                ClearData();
                ForceItemsRepeaterCleanup();
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
            if (e.Parameter is ShortcutPageNavParam param)
            {
                this._appName = param.AppName;
                this._shortcutFile = param.ShortcutFile;
                this.RebuildRows();
            }

            UnsubscribeFromEvents();
            PinnedShortcutsHelper.PinnedShortcutsChanged += this.OnPinnedShortcutsChanged;
            _isEventSubscribed = true;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            UnsubscribeFromEvents();
            ClearData();
            ForceItemsRepeaterCleanup();
        }

        public void ClearData()
        {
            // Clear the collection to trigger ElementClearing for all items
            this.Rows.Clear();
            _shortcutFile = null;
            _appName = string.Empty;
        }

        /// <summary>
        /// Forces ItemsRepeater to release all cached/recycled elements and clear WinUI's internal template cache.
        /// This addresses the ConcurrentDictionary WeakReference cache leak in WinUI.
        /// </summary>
        private void ForceItemsRepeaterCleanup()
        {
            if (this.MainItemsRepeater == null)
            {
                return;
            }

            try
            {
                // Get the parent ScrollViewer
                if (this.Content is ScrollViewer scrollViewer)
                {
                    // Create a brand new ItemsRepeater
                    var newRepeater = new ItemsRepeater
                    {
                        Name = "MainItemsRepeater",
                        Margin = new Thickness(0, 0, 0, 24),
                        ItemTemplate = this.Resources["RowTemplateSelector"] as IElementFactory,
                        Layout = new StackLayout(),
                    };

                    newRepeater.ElementClearing += MainItemsRepeater_ElementClearing;

                    scrollViewer.Content = newRepeater;
                }

                GC.Collect(2, GCCollectionMode.Aggressive, true, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Aggressive, true, true);
            }
            catch
            {
                // Fail silently
            }
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
