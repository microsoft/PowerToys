// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using ShortcutGuide.Models;

namespace ShortcutGuide.ViewModels
{
    /// <summary>
    /// View-model for a single labelled section in the unified shortcut list
    /// (Pinned, Recommended, a category, or Taskbar).
    /// </summary>
    public sealed class ShortcutSection : INotifyPropertyChanged
    {
        private int _itemCount;

        public string Title { get; set; } = string.Empty;

        public string? Subtitle { get; set; }

        public string? EmptyText { get; set; }

        public ObservableCollection<ShortcutEntry> Items { get; } = new();

        public bool HasItems => this._itemCount > 0;

        public bool ShowEmptyText => this._itemCount == 0 && !string.IsNullOrEmpty(this.EmptyText);

        public bool HasSubtitle => !string.IsNullOrEmpty(this.Subtitle);

        public event PropertyChangedEventHandler? PropertyChanged;

        public ShortcutSection()
        {
            this.Items.CollectionChanged += (_, _) =>
            {
                if (this._itemCount != this.Items.Count)
                {
                    this._itemCount = this.Items.Count;
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.HasItems)));
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.ShowEmptyText)));
                }
            };
        }
    }
}
