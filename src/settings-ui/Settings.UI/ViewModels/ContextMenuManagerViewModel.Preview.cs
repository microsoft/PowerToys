// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.Win32;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    // Emulated context menu: a best-effort rebuild of what Explorer shows for each kind of
    // right-click, from the same entries the list toggles. Explorer's own items come from a fixed
    // table (they aren't registry entries this tool edits), and a classic handler shows as one row
    // since the items it adds only exist at runtime - "Capture real menu" shows those.
    public partial class ContextMenuManagerViewModel
    {
        // Explorer's built-in items, by resw key suffix. A trailing '>' marks a submenu, "-" a separator.
        private static readonly Dictionary<ContextMenuPreviewTarget, string[]> BuiltInTop = new()
        {
            [ContextMenuPreviewTarget.Desktop] = new[] { "View>", "SortBy>", "Refresh", "-", "Paste", "PasteShortcut", "Undo" },
            [ContextMenuPreviewTarget.FolderBackground] = new[] { "View>", "SortBy>", "GroupBy>", "Refresh", "-", "Paste", "PasteShortcut", "Undo" },
            [ContextMenuPreviewTarget.Folder] = new[] { "Open", "OpenInNewWindow", "PinToQuickAccess" },
            [ContextMenuPreviewTarget.File] = new[] { "Open", "OpenWith>" },
            [ContextMenuPreviewTarget.Drive] = new[] { "Open", "OpenInNewWindow", "PinToQuickAccess" },
        };

        private static readonly Dictionary<ContextMenuPreviewTarget, string[]> BuiltInMiddle = new()
        {
            [ContextMenuPreviewTarget.Desktop] = new[] { "New>" },
            [ContextMenuPreviewTarget.FolderBackground] = new[] { "New>" },
            [ContextMenuPreviewTarget.Folder] = new[] { "SendTo>", "-", "Cut", "Copy", "-", "CreateShortcut", "Delete", "Rename" },
            [ContextMenuPreviewTarget.File] = new[] { "SendTo>", "-", "Cut", "Copy", "-", "CreateShortcut", "Delete", "Rename" },
            [ContextMenuPreviewTarget.Drive] = new[] { "Format", "-", "Copy", "-", "CreateShortcut", "Rename" },
        };

        private static readonly Dictionary<ContextMenuPreviewTarget, string[]> BuiltInEnd = new()
        {
            [ContextMenuPreviewTarget.Desktop] = Array.Empty<string>(),
            [ContextMenuPreviewTarget.FolderBackground] = new[] { "Properties" },
            [ContextMenuPreviewTarget.Folder] = new[] { "Properties" },
            [ContextMenuPreviewTarget.File] = new[] { "Properties" },
            [ContextMenuPreviewTarget.Drive] = new[] { "Properties" },
        };

        // Registry roots Explorer merges for each target. The sample file is a .txt, matching the capture helper.
        private static readonly Dictionary<ContextMenuPreviewTarget, string[]> TargetRoots = new()
        {
            [ContextMenuPreviewTarget.Desktop] = new[] { "DesktopBackground", "Directory\\Background" },
            [ContextMenuPreviewTarget.FolderBackground] = new[] { "Directory\\Background" },
            [ContextMenuPreviewTarget.Folder] = new[] { "Directory", "AllFilesystemObjects" },
            [ContextMenuPreviewTarget.File] = new[] { "*", "AllFilesystemObjects", ".txt" },
            [ContextMenuPreviewTarget.Drive] = new[] { "Drive", "AllFilesystemObjects" },
        };

        // Present when the user restored the Windows 10 menu as the default right-click menu.
        private const string ClassicMenuRestoreKey = "Software\\Classes\\CLSID\\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\\InprocServer32";

        private ContextMenuPreviewTarget _previewTarget = ContextMenuPreviewTarget.Desktop;
        private bool _isPreviewCaptured;

        public ObservableCollection<ContextMenuPreviewItem> PreviewItems { get; } = new ObservableCollection<ContextMenuPreviewItem>();

        public int PreviewTargetIndex
        {
            get => (int)_previewTarget;
            set
            {
                if (value >= 0 && value != (int)_previewTarget)
                {
                    _previewTarget = (ContextMenuPreviewTarget)value;
                    OnPropertyChanged(nameof(PreviewTargetIndex));
                    BuildSyntheticPreview();
                }
            }
        }

        public bool IsPreviewCaptured
        {
            get => _isPreviewCaptured;
            private set
            {
                if (value != _isPreviewCaptured)
                {
                    _isPreviewCaptured = value;
                    OnPropertyChanged(nameof(IsPreviewCaptured));
                    OnPropertyChanged(nameof(PreviewSourceText));
                }
            }
        }

        public string PreviewSourceText => ResourceLoaderInstance.ResourceLoader.GetString(
            IsPreviewCaptured ? "ContextMenuManager_PreviewSourceCaptured" : "ContextMenuManager_PreviewSourceSynthetic");

        public void BuildSyntheticPreview()
        {
            List<ContextMenuPreviewItem> classic = BuildClassicItems(_previewTarget);

            List<ContextMenuPreviewItem> items;
            if (IsClassicMenuDefault())
            {
                items = classic;
            }
            else
            {
                // Windows 11 menu: packaged items on top, everything classic behind "Show more options".
                items = EnabledEntries(_previewTarget)
                    .Where(e => e.Source == ContextMenuEntrySource.Modern)
                    .Select(ToPreviewItem)
                    .ToList();
                items.Add(ContextMenuPreviewItem.Separator());
                var more = BuiltIn("ShowMoreOptions");
                more.Children.AddRange(classic);
                items.Add(more);
            }

            ShowPreview(items, captured: false);
        }

        private void ShowPreview(IEnumerable<ContextMenuPreviewItem> items, bool captured)
        {
            PreviewItems.Clear();
            foreach (var item in TidySeparators(items))
            {
                PreviewItems.Add(item);
            }

            IsPreviewCaptured = captured;
        }

        private List<ContextMenuPreviewItem> BuildClassicItems(ContextMenuPreviewTarget target)
        {
            var entries = EnabledEntries(target).ToList();

            bool IsPosition(ContextMenuEntry e, string position) => string.Equals(e.Position, position, StringComparison.OrdinalIgnoreCase);

            var items = new List<ContextMenuPreviewItem>();
            items.AddRange(entries.Where(e => e.Kind == ContextMenuEntryKind.Verb && IsPosition(e, "Top")).OrderBy(e => e.HandlerKeyName, StringComparer.OrdinalIgnoreCase).Select(ToPreviewItem));
            items.AddRange(BuiltInItems(BuiltInTop[target]));
            items.Add(ContextMenuPreviewItem.Separator());

            // Explorer adds static verbs first, then packaged verbs, then shellex handlers, each in key-name order.
            items.AddRange(entries.Where(e => e.Kind == ContextMenuEntryKind.Verb && !IsPosition(e, "Top") && !IsPosition(e, "Bottom")).OrderBy(e => e.HandlerKeyName, StringComparer.OrdinalIgnoreCase).Select(ToPreviewItem));
            items.AddRange(entries.Where(e => e.Source == ContextMenuEntrySource.Modern).OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase).Select(ToPreviewItem));
            items.AddRange(entries.Where(e => e.Source == ContextMenuEntrySource.Classic && e.Kind != ContextMenuEntryKind.Verb && !BuiltInHandlerDenylist.Contains(e.HandlerKeyName)).OrderBy(e => e.HandlerKeyName, StringComparer.OrdinalIgnoreCase).Select(ToPreviewItem));

            items.Add(ContextMenuPreviewItem.Separator());
            items.AddRange(BuiltInItems(BuiltInMiddle[target]));
            items.Add(ContextMenuPreviewItem.Separator());
            items.AddRange(entries.Where(e => e.Kind == ContextMenuEntryKind.Verb && IsPosition(e, "Bottom")).OrderBy(e => e.HandlerKeyName, StringComparer.OrdinalIgnoreCase).Select(ToPreviewItem));
            items.Add(ContextMenuPreviewItem.Separator());
            items.AddRange(BuiltInItems(BuiltInEnd[target]));
            return items;
        }

        // Top-level entries that are enabled and registered for the target. Submenu items are reached
        // through their parent in ToPreviewItem.
        private IEnumerable<ContextMenuEntry> EnabledEntries(ContextMenuPreviewTarget target)
        {
            var roots = TargetRoots[target];
            return Entries.Where(e => e.IsEnabled && e.Parent == null
                && (e.Roots.Any(r => roots.Contains(r, StringComparer.OrdinalIgnoreCase))
                    || e.ItemTypes.Any(t => roots.Contains(t, StringComparer.OrdinalIgnoreCase))));
        }

        private ContextMenuPreviewItem ToPreviewItem(ContextMenuEntry entry)
        {
            var item = new ContextMenuPreviewItem
            {
                Text = entry.DisplayName,
                Icon = entry.Icon,
                Shortcut = entry.Source == ContextMenuEntrySource.Classic && entry.Kind != ContextMenuEntryKind.Verb
                    ? ResourceLoaderInstance.ResourceLoader.GetString("ContextMenuManager_PreviewHandlerHint")
                    : null,
            };

            foreach (var child in Entries.Where(e => e.Parent == entry && e.IsEnabled).OrderBy(e => e.HandlerKeyName, StringComparer.OrdinalIgnoreCase))
            {
                var childItem = ToPreviewItem(child);

                // Children carry "Parent > Child" in the list; the menu shows only their own label.
                childItem.Text = child.DisplayName.Substring(entry.DisplayName.Length + 3);
                item.Children.Add(childItem);
            }

            return item;
        }

        private static IEnumerable<ContextMenuPreviewItem> BuiltInItems(IEnumerable<string> keys) =>
            keys.Select(k => k == "-" ? ContextMenuPreviewItem.Separator() : BuiltIn(k));

        private static ContextMenuPreviewItem BuiltIn(string key)
        {
            bool submenu = key.EndsWith('>');
            var item = new ContextMenuPreviewItem
            {
                Text = ResourceLoaderInstance.ResourceLoader.GetString("ContextMenuManager_BuiltIn_" + key.TrimEnd('>')),
                IsBuiltIn = true,
            };

            if (submenu)
            {
                // Placeholder so the row shows a chevron; Explorer fills these at runtime.
                item.Children.Add(new ContextMenuPreviewItem { Text = "…", IsBuiltIn = true });
            }

            return item;
        }

        // Drops leading, trailing and doubled separators left behind by empty sections.
        private static IEnumerable<ContextMenuPreviewItem> TidySeparators(IEnumerable<ContextMenuPreviewItem> items)
        {
            var result = new List<ContextMenuPreviewItem>();
            foreach (var item in items)
            {
                if (item.IsSeparator && (result.Count == 0 || result[^1].IsSeparator))
                {
                    continue;
                }

                result.Add(item);
            }

            if (result.Count > 0 && result[^1].IsSeparator)
            {
                result.RemoveAt(result.Count - 1);
            }

            return result;
        }

        private static bool IsClassicMenuDefault()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(ClassicMenuRestoreKey, writable: false);
                return key != null;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
