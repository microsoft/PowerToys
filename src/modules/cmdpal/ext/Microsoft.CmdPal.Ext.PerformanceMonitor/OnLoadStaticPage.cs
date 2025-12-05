// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

internal abstract partial class OnLoadStaticPage : Page, IListPage
{
    private string _placeholderText = string.Empty;
    private string _searchText = string.Empty;
    private bool _showDetails;
    private bool _hasMore;
    private IFilters? _filters;
    private IGridProperties? _gridProperties;
    private ICommandItem? _emptyContent;
    private int _loadCount;

#pragma warning disable CS0067 // The event is never used

    private event TypedEventHandler<object, IItemsChangedEventArgs>? InternalItemsChanged;
#pragma warning restore CS0067 // The event is never used

    public event TypedEventHandler<object, IItemsChangedEventArgs> ItemsChanged
    {
        add
        {
            InternalItemsChanged += value;
            if (_loadCount == 0)
            {
                Loaded();
            }

            _loadCount++;
        }

        remove
        {
            InternalItemsChanged -= value;
            _loadCount--;
            _loadCount = Math.Max(0, _loadCount);
            if (_loadCount == 0)
            {
                Unloaded();
            }
        }
    }

    protected abstract void Loaded();

    protected abstract void Unloaded();

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

    public void LoadMore()
    {
    }

    protected void SetSearchNoUpdate(string newSearchText)
    {
        _searchText = newSearchText;
    }

    public abstract IListItem[] GetItems();
}


#pragma warning restore SA1402 // File may only contain a single type
