// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Microsoft.CmdPal.UI.ViewModels.Gallery;

public sealed partial class GallerySourceViewModel : ObservableObject
{
    public string Kind { get; }

    public string DisplayName { get; }

    public string? Id { get; }

    public string? Uri { get; }

    public bool IsKnown { get; }

    public ObservableCollection<GallerySourceDetailItemViewModel> Details { get; } = [];

    public bool HasDetails => Details.Count > 0;

    public GallerySourceViewModel(
        string kind,
        string displayName,
        string? id,
        string? uri,
        bool isKnown)
    {
        Kind = kind;
        DisplayName = displayName;
        Id = id;
        Uri = uri;
        IsKnown = isKnown;
        Details.CollectionChanged += OnDetailsCollectionChanged;
    }

    public void SetDetails(IReadOnlyList<GallerySourceDetailItemViewModel> details)
    {
        Details.Clear();
        for (var i = 0; i < details.Count; i++)
        {
            Details.Add(details[i]);
        }
    }

    private void OnDetailsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasDetails));
    }
}
