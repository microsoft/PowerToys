// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

#pragma warning disable SA1402 // File may only contain a single type

/// <summary>
/// Helper class for creating ListPage's which can listen for when they're
/// loaded and unloaded. This works because CmdPal will attach an event handler
/// to the ItemsChanged event when the page is added to the UI, and remove it
/// when the page is removed from the UI.
///
/// Subclasses should override the Loaded and Unloaded methods to start/stop
/// any background work needed to populate the page.
/// </summary>
internal abstract partial class OnLoadStaticListPage : OnLoadBasePage, IListPage
{
    private string _searchText = string.Empty;

    public virtual string PlaceholderText { get; set => SetProperty(ref field, value); } = string.Empty;

    public virtual string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }

    public virtual bool ShowDetails { get; set => SetProperty(ref field, value); }

    public virtual bool HasMoreItems { get; set => SetProperty(ref field, value); }

    public virtual IFilters? Filters { get; set => SetProperty(ref field, value); }

    public virtual IGridProperties? GridProperties { get; set => SetProperty(ref field, value); }

    public virtual ICommandItem? EmptyContent { get; set => SetProperty(ref field, value); }

    public void LoadMore()
    {
    }

    protected void SetSearchNoUpdate(string newSearchText)
    {
        _searchText = newSearchText;
    }

    public abstract IListItem[] GetItems();
}

/// <summary>
/// Helper class for creating ContentPage's which can listen for when they're
/// loaded and unloaded. This works because CmdPal will attach an event handler
/// to the ItemsChanged event when the page is added to the UI, and remove it
/// when the page is removed from the UI.
///
/// Subclasses should override the Loaded and Unloaded methods to start/stop
/// any background work needed to populate the page.
/// </summary>
internal abstract partial class OnLoadContentPage : OnLoadBasePage, IContentPage
{
    public virtual IDetails? Details { get; set => SetProperty(ref field, value); }

    public virtual IContextItem[] Commands { get; set => SetProperty(ref field, value); } = [];

    public abstract IContent[] GetContent();
}

internal abstract partial class OnLoadBasePage : Page
{
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

    protected void RaiseItemsChanged(int totalItems = -1)
    {
        try
        {
            // TODO #181 - This is the same thing that BaseObservable has to deal with.
            InternalItemsChanged?.Invoke(this, new ItemsChangedEventArgs(totalItems));
        }
        catch
        {
        }
    }
}


#pragma warning restore SA1402 // File may only contain a single type
