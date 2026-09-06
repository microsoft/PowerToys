// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// A contiguous run of grid tiles, preceded by an optional section or separator.
/// </summary>
[WinRT.GeneratedBindableCustomProperty([nameof(Title), nameof(Items), nameof(HasSectionCommand), nameof(SectionCommandAccessibleName), nameof(IsSectionCommandSelected)], [])]
public sealed partial class GridItemGroupViewModel : ObservableObject
{
    public ObservableCollection<ListItemViewModel> Items { get; } = [];

    public ListItemViewModel? Header { get; }

    public bool HasHeader => Header is not null;

    public string Title { get; private set; } = string.Empty;

    public bool IsSectionHeader { get; private set; }

    public bool IsSeparator { get; private set; }

    public string SectionCommandName { get; private set; } = string.Empty;

    public bool HasSectionCommand { get; private set; }

    public string SectionCommandAccessibleName => HasSectionCommand ? $"{Title}, {SectionCommandName}" : Title;

    public bool IsSectionCommandSelected { get; private set; }

    public ICommand? SectionCommand => Header?.InvokeSectionCommandCommand;

    public int FirstItemIndex { get; internal set; }

    internal int HeaderOccurrence { get; }

    internal GridItemGroupViewModel(ListItemViewModel? header, int headerOccurrence)
    {
        Header = header;
        HeaderOccurrence = headerOccurrence;
        RefreshHeader();
    }

    public void SetSectionCommandSelected(bool value)
    {
        value &= HasSectionCommand;
        if (IsSectionCommandSelected == value)
        {
            return;
        }

        IsSectionCommandSelected = value;
        OnPropertyChanged(nameof(IsSectionCommandSelected));
    }

    // Native group peers fall back to the content's plain-text representation
    // for unnamed groups, including separators and the headerless first group.
    public override string ToString() => Title;

    internal void RefreshHeader()
    {
        var title = Header?.Section ?? string.Empty;
        var isSectionHeader = Header?.Type == ListItemType.SectionHeader;
        var isSeparator = Header?.Type == ListItemType.Separator;
        var sectionCommandName = isSectionHeader ? Header?.SectionCommandName ?? string.Empty : string.Empty;
        var hasSectionCommand = isSectionHeader && Header?.HasSectionCommand == true;

        if (Title != title)
        {
            Title = title;
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(SectionCommandAccessibleName));
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

        if (SectionCommandName != sectionCommandName)
        {
            SectionCommandName = sectionCommandName;
            OnPropertyChanged(nameof(SectionCommandName));
            OnPropertyChanged(nameof(SectionCommandAccessibleName));
        }

        if (HasSectionCommand != hasSectionCommand)
        {
            HasSectionCommand = hasSectionCommand;
            OnPropertyChanged(nameof(HasSectionCommand));
            OnPropertyChanged(nameof(SectionCommandAccessibleName));
        }

        if (!hasSectionCommand && IsSectionCommandSelected)
        {
            SetSectionCommandSelected(false);
        }
    }
}
