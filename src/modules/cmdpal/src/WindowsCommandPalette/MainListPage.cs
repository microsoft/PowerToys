// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Specialized;
using CmdPal.Models;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using WindowsCommandPalette.Views;

namespace DeveloperCommandPalette;

public sealed class MainListPage : DynamicListPage
{
    private readonly MainViewModel _mainViewModel;
    private readonly MainListSection _mainSection;
    private readonly RecentsListSection _recentsListSection;
    private readonly FilteredListSection _filteredSection;
    private readonly ISection[] _sections;

    public MainListPage(MainViewModel viewModel)
    {
        this._mainViewModel = viewModel;

        _mainSection = new(_mainViewModel);
        _recentsListSection = new(_mainViewModel);
        _filteredSection = new(_mainViewModel);

        _mainViewModel.TopLevelCommands.CollectionChanged += TopLevelCommands_CollectionChanged;

        _sections = [
            _recentsListSection,
            _mainSection
        ];

        PlaceholderText = "Search...";
        ShowDetails = true;
        Loading = false;
    }

    public override ISection[] GetItems()
    {
        return _sections;
    }

    public override ISection[] GetItems(string query)
    {
        _filteredSection.Query = query;
        _mainSection.UpdateQuery(query);
        if (string.IsNullOrEmpty(query))
        {
            return _sections;
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

                    _filteredSection.TopLevelItems.Add(new MainListItem(listItem.Unsafe));
                }
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
        {
            foreach (var item in e.OldItems)
            {
                if (item is ExtensionObject<IListItem> listItem)
                {
                    foreach (var mainListItem in _mainSection._Items)
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
