// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CmdPal.Extensions.Helpers;

public class ListPage : Page, IListPage
{
    private string _placeholderText = string.Empty;
    private string _searchText = string.Empty;
    private bool _showDetails;
    private bool _hasMore;
    private IFilters? _filters;
    private IGridProperties? _gridProperties;

    public event TypedEventHandler<object, ItemsChangedEventArgs>? ItemsChanged;

    public string PlaceholderText
    {
        get => _placeholderText;
        set
        {
            _placeholderText = value;
            OnPropertyChanged(nameof(PlaceholderText));
        }
    }

    public virtual string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged(nameof(SearchText));
        }
    }

    public bool ShowDetails
    {
        get => _showDetails;
        set
        {
            _showDetails = value;
            OnPropertyChanged(nameof(ShowDetails));
        }
    }

    public bool HasMore
    {
        get => _hasMore;
        set
        {
            _hasMore = value;
            OnPropertyChanged(nameof(HasMore));
        }
    }

    public IFilters? Filters
    {
        get => _filters;
        set
        {
            _filters = value;
            OnPropertyChanged(nameof(Filters));
        }
    }

    public IGridProperties? GridProperties
    {
        get => _gridProperties;
        set
        {
            _gridProperties = value;
            OnPropertyChanged(nameof(GridProperties));
        }
    }

    public virtual IListItem[] GetItems() => [];

    public virtual void LoadMore()
    {
    }

    protected void RaiseItemsChanged(int totalItems)
    {
        if (ItemsChanged != null)
        {
            ItemsChanged.Invoke(this, new ItemsChangedEventArgs(totalItems));
        }
    }
}
