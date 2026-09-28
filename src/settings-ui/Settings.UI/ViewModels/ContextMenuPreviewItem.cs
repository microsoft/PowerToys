// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using Microsoft.UI.Xaml.Media;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    // Which right-click the preview emulates.
    public enum ContextMenuPreviewTarget
    {
        Desktop,
        FolderBackground,
        Folder,
        File,
        Drive,
    }

    // One row of the emulated context menu. Built either from the enumerated entries (synthetic
    // preview) or from a real menu captured by the MenuCapture helper process.
    public class ContextMenuPreviewItem
    {
        public string Text { get; set; }

        // Right-aligned text: an accelerator ("Ctrl+Z") or a hint for handler rows.
        public string Shortcut { get; set; }

        public ImageSource Icon { get; set; }

        public bool IsSeparator { get; set; }

        // Explorer's own items, emulated from a fixed list; drawn dimmed since they aren't editable here.
        public bool IsBuiltIn { get; set; }

        public bool IsDisabled { get; set; }

        public List<ContextMenuPreviewItem> Children { get; } = new List<ContextMenuPreviewItem>();

        public bool HasChildren => Children.Count > 0;

        public bool IsItem => !IsSeparator;

        public double TextOpacity => IsBuiltIn || IsDisabled ? 0.6 : 1.0;

        public static ContextMenuPreviewItem Separator() => new ContextMenuPreviewItem { IsSeparator = true };
    }
}
