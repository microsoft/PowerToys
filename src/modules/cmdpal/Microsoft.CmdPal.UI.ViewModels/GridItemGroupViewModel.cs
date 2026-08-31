// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// A contiguous run of grid tiles, preceded by an optional section or separator.
/// </summary>
[WinRT.GeneratedBindableCustomProperty([nameof(Title), nameof(Items)], [])]
public sealed partial class GridItemGroupViewModel : ObservableObject
{
    public ObservableCollection<ListItemViewModel> Items { get; } = [];

    public ListItemViewModel? Header { get; }

    public bool HasHeader => Header is not null;

    public string Title { get; private set; } = string.Empty;

    public bool IsSectionHeader { get; private set; }

    public bool IsSeparator { get; private set; }

    public int FirstItemIndex { get; internal set; }

    internal int HeaderOccurrence { get; }

    internal GridItemGroupViewModel(ListItemViewModel? header, int headerOccurrence)
    {
        Header = header;
        HeaderOccurrence = headerOccurrence;
        RefreshHeader();
    }

    // Native group peers fall back to the content's plain-text representation
    // for unnamed groups, including separators and the headerless first group.
    public override string ToString() => Title;

    internal void RefreshHeader()
    {
        var title = Header?.Section ?? string.Empty;
        var isSectionHeader = Header?.Type == ListItemType.SectionHeader;
        var isSeparator = Header?.Type == ListItemType.Separator;

        if (Title != title)
        {
            Title = title;
            OnPropertyChanged(nameof(Title));
        }

        if (IsSectionHeader != isSectionHeader)
        {
            IsSectionHeader = isSectionHeader;
            OnPropertyChanged(nameof(IsSectionHeader));
        }

        if (IsSeparator != isSeparator)
        {
            IsSeparator = isSeparator;
            OnPropertyChanged(nameof(IsSeparator));
        }
    }
}
