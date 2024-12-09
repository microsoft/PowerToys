// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Microsoft.UI.Dispatching;

namespace WindowsCommandPalette.Views;

public sealed class ListPageViewModel : PageViewModel, INotifyPropertyChanged
{
    private readonly ObservableCollection<ListItemViewModel> _items = [];

    public ObservableCollection<ListItemViewModel> FilteredItems { get; set; } = [];

    internal IListPage Page => (IListPage)this.PageAction;

    private IDynamicListPage? IsDynamicPage => Page as IDynamicListPage;

    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private string _query = string.Empty;

    private bool _forceShowDetails;

    public bool ShowDetails => _forceShowDetails || Page.ShowDetails;

    public bool HasMoreItems { get; private set; }

    public string PlaceholderText { get; private set; } = "Type here to search...";

    public string SearchText { get; set; } = string.Empty;

    private bool _loadingMore;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ListPageViewModel(IListPage page)
        : base(page)
    {
        page.PropChanged += Page_PropChanged;
        page.ItemsChanged += Page_ItemsChanged;

        HasMoreItems = page.HasMoreItems;
        PlaceholderText = page.PlaceholderText;
    }

    private void Page_ItemsChanged(object sender, ItemsChangedEventArgs args)
    {
        Debug.WriteLine("Items changed");

        _loadingMore = false;

        _dispatcherQueue.TryEnqueue(async () =>
        {
            await this.UpdateListItems();
        });
    }

    private void Page_PropChanged(object sender, PropChangedEventArgs args)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            switch (args.PropertyName)
            {
                case nameof(HasMoreItems):
                    HasMoreItems = Page.HasMoreItems;
                    break;
                case nameof(PlaceholderText):
                    PlaceholderText = Page.PlaceholderText;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PlaceholderText)));
                    break;
                case nameof(SearchText):
                    SearchText = Page.SearchText;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SearchText)));
                    break;
            }
        });
    }

    internal Task InitialRender() => UpdateListItems();

    internal async Task UpdateListItems()
    {
        // on main thread
        var t = new Task<IListItem[]>(() =>
        {
            try
            {
                return this.Page.GetItems();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                _forceShowDetails = true;
                return [new ErrorListItem(ex)];
            }
        });
        t.Start();
        var items = await t;

        // still on main thread

        // This creates an entirely new list of ListItemViewModels, and we're
        // really hoping that the equality check in `InPlaceUpdateList`
        // properly uses ListItemViewModel.Equals to compare if the objects
        // are literally the same.
        Collection<ListItemViewModel> newItems = [.. items.Select(i => new ListItemViewModel(i)).ToList()];

        // Debug.WriteLine($"  Found {newItems.Count} items");

        // THIS populates FilteredItems. If you do this off the UI thread, guess what -
        // the list view won't update. So WATCH OUT
        ListHelpers.InPlaceUpdateList(FilteredItems, newItems);
        ListHelpers.InPlaceUpdateList(_items, newItems);

        // Debug.WriteLine($"Done with UpdateListItems, found {FilteredItems.Count} / {_items.Count}");
    }

    public void UpdateSearchText(string query)
    {
        if (query == _query)
        {
            return;
        }

        _query = query;
        if (IsDynamicPage != null)
        {
            // Tell the dynamic page the new search text. If they need to update, they will.
            IsDynamicPage.SearchText = _query;
        }
        else
        {
            var filtered = ListItemViewModel
                .FilterList(_items, query);
            Collection<ListItemViewModel> newItems = [.. filtered.ToList()];
            ListHelpers.InPlaceUpdateList(FilteredItems, newItems);
        }
    }

    public async void LoadMoreIfNeeded()
    {
        if (!_loadingMore && HasMoreItems)
        {
            // This is kinda a hack, to prevent us from over-requesting
            // more at the bottom.
            // We'll set this flag after we've requested more. We will clear it
            // on the new ItemsChanged
            _loadingMore = true;

            // TODO GH #73: When we have a real prototype, this should be an async call
            // A thought: maybe the ExtensionObject.Unsafe could be an async
            // call, so that you _know_ you need to wrap it up when you call it?
            var t = new Task(Page.LoadMore);
            t.Start();
            await t;
        }
    }
}
