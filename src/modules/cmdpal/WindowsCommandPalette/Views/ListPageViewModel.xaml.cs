// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Microsoft.UI.Dispatching;

namespace WindowsCommandPalette.Views;

public sealed class ListPageViewModel : PageViewModel
{
    private readonly ObservableCollection<ListItemViewModel> _items = [];

    public ObservableCollection<ListItemViewModel> FilteredItems { get; set; } = [];

    internal IListPage Page => (IListPage)this.PageAction;

    private bool IsDynamic => Page is IDynamicListPage;

    private IDynamicListPage? IsDynamicPage => Page as IDynamicListPage;

    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private string _query = string.Empty;

    private bool _forceShowDetails;

    public bool ShowDetails => _forceShowDetails || Page.ShowDetails;

    public ListPageViewModel(IListPage page)
        : base(page)
    {
        page.PropChanged += Page_PropChanged;
    }

    private void Page_PropChanged(object sender, PropChangedEventArgs args)
    {
        if (args.PropertyName == "Items")
        {
            Debug.WriteLine("Items changed");
            _dispatcherQueue.TryEnqueue(async () =>
            {
                await this.UpdateListItems();
            });
        }
    }

    internal Task InitialRender()
    {
        return UpdateListItems();
    }

    internal async Task UpdateListItems()
    {
        // on main thread
        var t = new Task<IListItem[]>(() =>
        {
            try
            {
                return IsDynamicPage != null ?
                    IsDynamicPage.GetItems(_query) :
                    this.Page.GetItems();
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

        // TODO! For dynamic lists, we're clearing out the whole list of items
        // we already have, then rebuilding it. We shouldn't do that. We should
        // still use the results from GetItems and put them into the code in
        // UpdateFilter to intelligently add/remove as needed.
        // TODODO! are we still? ^^
        Collection<ListItemViewModel> newItems = new(items.Select(i => new ListItemViewModel(i)).ToList());
        Debug.WriteLine($"  Found {newItems.Count} items");

        // THIS populates FilteredItems. If you do this off the UI thread, guess what -
        // the list view won't update. So WATCH OUT
        ListHelpers.InPlaceUpdateList(FilteredItems, newItems);
        ListHelpers.InPlaceUpdateList(_items, newItems);

        Debug.WriteLine($"Done with UpdateListItems, found {FilteredItems.Count} / {_items.Count}");
    }

    internal async Task<IEnumerable<ListItemViewModel>> GetFilteredItems(string query)
    {
        // This method does NOT change any lists. It doesn't modify _items or FilteredItems...
        if (query == _query)
        {
            return FilteredItems;
        }

        _query = query;
        if (IsDynamic)
        {
            // ... except here we might modify those lists. But ignore that for now, GH #77 will fix this.
            await UpdateListItems();
            return FilteredItems;
        }
        else
        {
            // Static lists don't need to re-fetch the items
            if (string.IsNullOrEmpty(query))
            {
                return _items;
            }

            // TODO! Probably bad that this turns list view models into listitems back to NEW view models
            // TODO! make this safer
            // TODODO! ^ still relevant?
            var newFilter = ListHelpers
                .FilterList(_items.Select(vm => vm.ListItem.Unsafe), query)
                .Select(li => new ListItemViewModel(li));

            return newFilter;
        }
    }
}
