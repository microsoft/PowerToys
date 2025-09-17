// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

// Base implementation of IListContent for extensions to derive from when exposing
// list/grid data inside a ContentPage.
public partial class ListContent : BaseObservable, IListContent
{
    private string _placeholderText = string.Empty;
    private string _searchText = string.Empty;
    private bool _hasMore;
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
        protected set
        {
            _searchText = value;
            OnPropertyChanged(nameof(SearchText));
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
            ItemsChanged?.Invoke(this, new ItemsChangedEventArgs(totalItems));
        }
        catch
        {
        }
    }

    protected void SetSearchNoUpdate(string newSearchText)
    {
        _searchText = newSearchText;
    }
}

// Dynamic variant allows the host to set SearchText.
// Dynamic variant declared in separate file per style guidelines.
