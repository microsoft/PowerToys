// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CmdPal.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using WindowsCommandPalette.Views;

namespace DeveloperCommandPalette;

// The FilteredListSection is for when we've got any filter at all. It starts by
// enumerating all actions and apps, and returns the subset that matches.
public sealed class FilteredListSection : ISection, INotifyCollectionChanged
{
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public string Title => string.Empty;

    private readonly MainViewModel _mainViewModel;
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

    // Top-level list items, from builtin commands and extensions
    public ObservableCollection<MainListItem> TopLevelItems { get; set; }

    // Apps, from the apps built in command.
    private IEnumerable<IListItem> AllApps => _mainViewModel.apps.GetItems().First().Items;

    // Results from the last searched text
    private IEnumerable<IListItem>? lastSearchResults;

    // Things we should enumerate over, depending on the search query.
    // This is either
    // * the last search results (if there was a query),
    // * OR one of:
    //   * Just the top-level actions (if there's no query)
    //   * OR the top-level actions AND the apps (if there's a query)
    private IEnumerable<IListItem> ItemsToEnumerate =>
        lastSearchResults != null ?
            lastSearchResults :
            TopLevelItems.Concat(AllApps);

    private string _lastQuery = string.Empty;

    // Setting this will enumerate all the actions and installed apps.
    internal string Query
    {
        get => _lastQuery;
        set
        {
            if (string.IsNullOrEmpty(value) ||
                !_lastQuery.StartsWith(value, true, System.Globalization.CultureInfo.CurrentCulture))
            {
                lastSearchResults = null;
            }

            if (lastSearchResults != null && string.IsNullOrEmpty(value))
            {
                // Even with an empty query, we did filter the items (to remove fallbacks without a name
                // Here, we're going from an empty query to one that has an actual string. Reset the last, so we will _also_ include apps now.
                this.lastSearchResults = null;
            }

            _lastQuery = value;
            var results = ListHelpers.FilterList(ItemsToEnumerate, Query);
            this.lastSearchResults = string.IsNullOrEmpty(value) ? null : results;
        }
    }

    // Watch out future me!
    //
    // Don't do the whole linq query in Items itself. That'll evaluate the whole
    // query once per item, because the ListPage.xaml.cs will create one
    // ListViewItems per item in Items, and every time it does that, it calls
    // section.Items.
    //
    // instead run the query once when the action query changes, and store the
    // results.
    public IListItem[] Items => ItemsToEnumerate.Where(i => i != null).ToArray();

    public FilteredListSection(MainViewModel viewModel)
    {
        this._mainViewModel = viewModel;

        // TODO: We should probably just get rid of MainListItem entirely, so I'm leaveing these uncaught
        TopLevelItems = new(_mainViewModel.TopLevelCommands.Where(wrapper => wrapper.Unsafe != null).Select(wrapper => new MainListItem(wrapper.Unsafe!)));
        TopLevelItems.CollectionChanged += Bubble_CollectionChanged;
    }

    private void Bubble_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            CollectionChanged?.Invoke(this, e);
        });
    }

    internal void Reset()
    {
        TopLevelItems.Clear();
        lastSearchResults = null;
    }
}
