// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using WindowsCommandPalette.Models;
using WindowsCommandPalette.Views;

namespace WindowsCommandPalette;

public sealed partial class MainListPage : DynamicListPage
{
    private readonly MainViewModel _mainViewModel;

    private readonly FilteredListSection _filteredSection;
    private readonly ObservableCollection<MainListItem> topLevelItems = new();

    public MainListPage(MainViewModel viewModel)
    {
        this._mainViewModel = viewModel;

        // wacky: "All apps" is added to _mainViewModel.TopLevelCommands before
        // we're constructed, so we never get a
        // TopLevelCommands_CollectionChanged callback when we're first launched
        // that would let us add it
        foreach (var i in _mainViewModel.TopLevelCommands)
        {
            this.topLevelItems.Add(new MainListItem(i.Unsafe));
        }

        // We're using a FilteredListSection to help abstract some of dealing with
        // filtering the list of commands & apps. It's just a little more convenient.
        // It's not an actual section, just vestigial from that era.
        //
        // Let the FilteredListSection use our TopLevelItems. That way we don't
        // need to maintain two lists.
        _filteredSection = new(_mainViewModel, this.topLevelItems);

        // Listen for changes to the TopLevelCommands. This happens as we async
        // load them on startup. We'll use CollectionChanged as an opportunity
        // to raise the 'Items' changed event.
        _mainViewModel.TopLevelCommands.CollectionChanged += TopLevelCommands_CollectionChanged;

        PlaceholderText = "Search...";
        ShowDetails = true;
        Loading = false;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        UpdateQuery();
    }

    private void UpdateQuery()
    {
        // Let our filtering wrapper know the newly typed search text:
        _filteredSection.Query = SearchText;

        // Update all the top-level commands which are fallback providers:
        var fallbacks = topLevelItems
            .Select(i => i?.FallbackHandler)
            .Where(fb => fb != null)
            .Select(fb => fb!);

        foreach (var fb in fallbacks)
        {
            fb.UpdateQuery(SearchText);
        }

        var count = string.IsNullOrEmpty(SearchText) ? topLevelItems.Count : _filteredSection.Count;
        RaiseItemsChanged(count);
    }

    public override IListItem[] GetItems()
    {
        if (string.IsNullOrEmpty(SearchText))
        {
            return topLevelItems.ToArray();
        }
        else
        {
            return _filteredSection.Items;
        }
    }

    private void TopLevelCommands_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Debug.WriteLine("TopLevelCommands_CollectionChanged");
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            foreach (var item in e.NewItems)
            {
                if (item is ExtensionObject<IListItem> listItem)
                {
                    topLevelItems.Add(new MainListItem(listItem.Unsafe));
                }
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
        {
            foreach (var item in e.OldItems)
            {
                if (item is ExtensionObject<IListItem> _)
                {
                    // If we were maintaining the POC project we'd remove the items here.
                }
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            topLevelItems.Clear();
        }

        // Sneaky?
        // Raise a Items changed event, so the list page knows that our items
        // have changed, and it should re-fetch them.
        this.RaiseItemsChanged(topLevelItems.Count);
    }
}
