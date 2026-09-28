// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.UI.Xaml.Media;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    // Which right-click the preview shows.
    public enum ContextMenuPreviewTarget
    {
        Desktop,
        FolderBackground,
        Folder,
        File,
        Drive,
    }

    // One row of the context menu captured by the MenuCapture helper process, linked to the entry
    // that adds it when one could be matched.
    public class ContextMenuPreviewItem
    {
        private ImageSource _icon;

        public string Text { get; set; }

        // Right-aligned accelerator, e.g. "Ctrl+Z".
        public string Shortcut { get; set; }

        // Canonical verb the menu reports for the item; for a static verb, its registry key name.
        public string Verb { get; set; }

        // The item's own bitmap from the captured menu, else the entry's icon (which may load later).
        public ImageSource Icon
        {
            get => _icon ?? Entry?.Icon;
            set => _icon = value;
        }

        public bool IsSeparator { get; set; }

        // Section caption, e.g. above the entries that are turned off.
        public bool IsHeader { get; set; }

        // Windows 11's "Show more options" row, which switches the preview to the classic menu.
        public bool IsShowMoreOptions { get; set; }

        public bool IsDisabled { get; set; }

        // The registry entry that adds this item; null for Explorer's own items and unmatched ones.
        public ContextMenuEntry Entry { get; set; }

        public List<ContextMenuPreviewItem> Children { get; } = new List<ContextMenuPreviewItem>();

        public bool HasChildren => Children.Count > 0;

        public bool IsItem => !IsSeparator && !IsHeader;

        public bool IsOff => Entry != null && !Entry.IsEnabled;

        public string SideText => IsOff ? ResourceLoaderInstance.ResourceLoader.GetString("ContextMenuManager_PreviewOff") : Shortcut;

        public double TextOpacity => IsOff ? 0.45 : IsDisabled ? 0.6 : 1.0;

        public static ContextMenuPreviewItem Separator() => new ContextMenuPreviewItem { IsSeparator = true };
    }
}
