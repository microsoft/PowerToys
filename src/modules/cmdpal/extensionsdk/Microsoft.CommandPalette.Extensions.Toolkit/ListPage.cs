// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class ListPage : Page, IListPage
{
    private string _placeholderText = string.Empty;
    private string _searchText = string.Empty;
    private bool _showDetails;
    private bool _hasMore;
    private IFilters? _filters;
    private IGridProperties? _gridProperties;
    private ICommandItem? _emptyContent;

    public event TypedEventHandler<object, IItemsChangedEventArgs>? ItemsChanged;

    public virtual string PlaceholderText
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

    public virtual bool ShowDetails
    {
        get => _showDetails;
        set
        {
            _showDetails = value;
            OnPropertyChanged(nameof(ShowDetails));
        }
    }

    public virtual bool HasMoreItems
    {
        get => _hasMore;
        set
        {
            _hasMore = value;
            OnPropertyChanged(nameof(HasMoreItems));
        }
    }

    public virtual IFilters? Filters
    {
        get => _filters;
        set
        {
            _filters = value;
            OnPropertyChanged(nameof(Filters));
        }
    }

    public virtual IGridProperties? GridProperties
    {
        get => _gridProperties;
        set
        {
            _gridProperties = value;
            OnPropertyChanged(nameof(GridProperties));
        }
    }

    public virtual ICommandItem? EmptyContent
    {
        get => _emptyContent;
        set
        {
            _emptyContent = value;
            OnPropertyChanged(nameof(EmptyContent));
        }
    }

    public virtual IListItem[] GetItems() => [];

    public virtual void LoadMore()
    {
    }

    protected void RaiseItemsChanged(int totalItems = -1)
    {
        try
        {
            // TODO #181 - This is the same thing that BaseObservable has to deal with.
            ItemsChanged?.Invoke(this, new ItemsChangedEventArgs(totalItems));
        }
        catch
        {
        }
    }
}
