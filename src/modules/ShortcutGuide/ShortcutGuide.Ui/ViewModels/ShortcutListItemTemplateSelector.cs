// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ShortcutGuide.ViewModels
{
    /// <summary>
    /// Selects the correct DataTemplate for a flat shortcut-list row.
    /// </summary>
    public sealed partial class ShortcutListItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? HeaderTemplate { get; set; }

        public DataTemplate? SubtitleTemplate { get; set; }

        public DataTemplate? ShortcutTemplate { get; set; }

        public DataTemplate? EmptyTextTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item) =>
            (item as ShortcutListItem)?.Kind switch
            {
                ShortcutListItemKind.Header => this.HeaderTemplate!,
                ShortcutListItemKind.Subtitle => this.SubtitleTemplate!,
                ShortcutListItemKind.EmptyText => this.EmptyTextTemplate!,
                _ => this.ShortcutTemplate!,
            };

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container) =>
            this.SelectTemplateCore(item);
    }
}
