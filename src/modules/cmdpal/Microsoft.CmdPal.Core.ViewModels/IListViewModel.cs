// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Windows.Foundation;

namespace Microsoft.CmdPal.Core.ViewModels;

public interface IListViewModel : IDisposable
{
    ObservableCollection<ListItemViewModel> FilteredItems { get; set; }

    event TypedEventHandler<IListViewModel, object>? ItemsUpdated;

    bool IsNested { get; set; }

    bool ShowEmptyContent { get; }

    bool IsGridView { get; }

    IGridPropertiesViewModel? GridProperties { get; }

    bool ShowDetails { get; }

    string SearchText { get; }

    string InitialSearchText { get; }

    CommandItemViewModel EmptyContent { get; }

    bool IsInitialized { get; }

    void InitializeProperties();

    void LoadMoreIfNeeded();
}
