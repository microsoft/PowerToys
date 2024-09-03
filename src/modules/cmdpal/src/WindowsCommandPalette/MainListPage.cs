// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CmdPal.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.CommandPalette.Extensions;
using Microsoft.Windows.CommandPalette.Extensions.Helpers;
using WindowsCommandPalette.Views;

namespace DeveloperCommandPalette;

public sealed class RecentsListSection : ListSection, INotifyCollectionChanged
{
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    private readonly DispatcherQueue DispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private readonly MainViewModel _mainViewModel;

    internal ObservableCollection<MainListItem> _Items { get; set; } = [];

    private bool loadedApps;

    public RecentsListSection(MainViewModel viewModel)
    {
        this.Title = "Recent";
        this._mainViewModel = viewModel;

        var recent = _mainViewModel.RecentActions;
        Reset();
        _Items.CollectionChanged += Bubble_CollectionChanged;

        _mainViewModel.AppsReady += _mainViewModel_AppsReady;
    }

    private void _mainViewModel_AppsReady(object sender, object? args)
    {
        loadedApps = true;
        _mainViewModel.AppsReady -= _mainViewModel_AppsReady;
        AddApps();
    }

    private void Bubble_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            CollectionChanged?.Invoke(this, e);
        });
    }

    public override IListItem[] Items => _Items.ToArray();

    internal void Reset()
    {
        _Items.Clear();
        if (loadedApps)
        {
            AddApps();
        }
    }

    internal void AddApps()
    {
        var apps = _mainViewModel.Recent;
        foreach (var app in apps)
        {
            _Items.Add(new MainListItem(app.Unsafe)); // we know these are all local
        }
    }
}

// The MainListSection is for all non-recent actions. No apps.
public sealed class MainListSection : ISection, INotifyCollectionChanged
{
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public string Title => "Actions";

    private readonly MainViewModel _mainViewModel;
    private readonly DispatcherQueue DispatcherQueue = DispatcherQueue.GetForCurrentThread();

    // Top-level list items, from builtin commands and extensions
    internal ObservableCollection<MainListItem> _Items { get; set; }

    // Things we should enumerate over, depending on the search query.
    // This is either
    // * the last search results (if there was a query),
    // * OR one of:
    //   * Just the top-level actions (if there's no query)
    //   * OR the top-level actions AND the apps (if there's a query)
    private IEnumerable<IListItem> itemsToEnumerate =>
        _Items.Where(i => i != null && (!_mainViewModel.IsRecentCommand(i)));

    // Watch out future me!
    //
    // Don't do the whole linq query in Items itself. That'll evaluate the whole
    // query once per item, because the ListPage.xaml.cs will create one
    // ListViewItems per item in Items, and every time it does that, it calls
    // section.Items.
    //
    // instead run the query once when the action query changes, and store the
    // results.
    public IListItem[] Items => itemsToEnumerate.ToArray();

    public MainListSection(MainViewModel viewModel)
    {
        this._mainViewModel = viewModel;
        _Items = new(_mainViewModel.TopLevelCommands.Select(w => w.Unsafe).Where(li => li != null).Select(li => new MainListItem(li!)));
        _Items.CollectionChanged += Bubble_CollectionChanged;
    }

    internal void UpdateQuery(string query)
    {
        var fallbacks = _Items.Select(i => i?.FallbackHandler).Where(fb => fb != null).Select(fb => fb!);
        foreach (var fb in fallbacks)
        {
            fb.UpdateQuery(query);
        }
    }

    private void Bubble_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            CollectionChanged?.Invoke(this, e);
        });
    }

    internal void Reset()
    {
        _Items.Clear();
    }
}

// The FilteredListSection is for when we've got any filter at all. It starts by
// enumerating all actions and apps, and returns the subset that matches.
public sealed class FilteredListSection : ISection, INotifyCollectionChanged
{
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public string Title => string.Empty;

    private readonly MainViewModel _mainViewModel;
    private readonly DispatcherQueue DispatcherQueue = DispatcherQueue.GetForCurrentThread();

    // Top-level list items, from builtin commands and extensions
    internal ObservableCollection<MainListItem> _Items { get; set; }

    // Apps, from the apps built in command.
    private IEnumerable<IListItem> _apps => _mainViewModel.apps.GetItems().First().Items;

    // Results from the last searched text
    private IEnumerable<IListItem>? lastSearchResults;

    // Things we should enumerate over, depending on the search query.
    // This is either
    // * the last search results (if there was a query),
    // * OR one of:
    //   * Just the top-level actions (if there's no query)
    //   * OR the top-level actions AND the apps (if there's a query)
    private IEnumerable<IListItem> itemsToEnumerate =>
        lastSearchResults != null ?
            lastSearchResults :
            _Items.Concat(_apps);

    internal string lastQuery = string.Empty;

    // Setting this will enumerate all the actions and installed apps.
    internal string Query
    {
        get => lastQuery;
        set
        {
            if (string.IsNullOrEmpty(value) ||
                !lastQuery.StartsWith(value, true, System.Globalization.CultureInfo.CurrentCulture))
            {
                lastSearchResults = null;
            }

            if (lastSearchResults != null && string.IsNullOrEmpty(value))
            {
                // Even with an empty query, we did filter the items (to remove fallbacks without a name
                // Here, we're going from an empty query to one that has an actual string. Reset the last, so we will _also_ include apps now.
                this.lastSearchResults = null;
            }

            lastQuery = value;
            var results = ListHelpers.FilterList(itemsToEnumerate, Query);
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
    public IListItem[] Items => itemsToEnumerate.Where(i => i != null).ToArray();

    public FilteredListSection(MainViewModel viewModel)
    {
        this._mainViewModel = viewModel;

        // TODO: We should probably just get rid of MainListItem entirely, so I'm leaveing these uncaught
        _Items = new(_mainViewModel.TopLevelCommands.Where(wrapper => wrapper.Unsafe != null).Select(wrapper => new MainListItem(wrapper.Unsafe!)));
        _Items.CollectionChanged += Bubble_CollectionChanged;
    }

    private void Bubble_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            CollectionChanged?.Invoke(this, e);
        });
    }

    internal void Reset()
    {
        _Items.Clear();
        lastSearchResults = null;
    }
}

public sealed class MainListPage : Microsoft.Windows.CommandPalette.Extensions.Helpers.DynamicListPage
{
    private readonly MainViewModel _mainViewModel;
    private readonly MainListSection _mainSection;
    private readonly RecentsListSection _recentsListSection;
    private readonly FilteredListSection _filteredSection;
    private readonly ISection[] _Sections;

    public MainListPage(MainViewModel viewModel)
    {
        this._mainViewModel = viewModel;

        _mainSection = new(_mainViewModel);
        _recentsListSection = new(_mainViewModel);
        _filteredSection = new(_mainViewModel);

        _mainViewModel.TopLevelCommands.CollectionChanged += TopLevelCommands_CollectionChanged;

        _Sections = [
            _recentsListSection,
            _mainSection
        ];

        PlaceholderText = "Search...";
        ShowDetails = true;
        Loading = false;
    }

    public override ISection[] GetItems()
    {
        return _Sections;
    }

    public override ISection[] GetItems(string query)
    {
        _filteredSection.Query = query;
        _mainSection.UpdateQuery(query);
        if (string.IsNullOrEmpty(query))
        {
            return _Sections;
        }
        else
        {
            return [_filteredSection];
        }
    }

    private void TopLevelCommands_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            foreach (var item in e.NewItems)
            {
                if (item is ExtensionObject<IListItem> listItem)
                {
                    // Eh, it's fine to be unsafe here, we're probably tossing MainListItem
                    if (!_mainViewModel.Recent.Contains(listItem))
                    {
                        _mainSection._Items.Add(new MainListItem(listItem.Unsafe));
                    }

                    _filteredSection._Items.Add(new MainListItem(listItem.Unsafe));
                }
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
        {
            foreach (var item in e.OldItems)
            {
                if (item is ExtensionObject<IListItem> listItem)
                {
                    foreach (var mainListItem in _mainSection._Items) // MainListItem
                    {
                        if (mainListItem.Item == listItem)
                        {
                            _mainSection._Items.Remove(mainListItem);
                            break;
                        }
                    }
                }
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            _mainSection.Reset();
            _filteredSection.Reset();
        }

        _recentsListSection.Reset();
    }
}
