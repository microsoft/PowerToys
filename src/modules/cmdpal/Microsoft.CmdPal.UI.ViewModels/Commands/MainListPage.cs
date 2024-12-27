// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.CmdPal.UI.ViewModels.MainPage;

/// <summary>
/// This class encapsulates the data we load from built-in providers and extensions to use within the same extension-UI system for a <see cref="ListPage"/>.
/// TODO: Need to think about how we structure/interop for the page -> section -> item between the main setup, the extensions, and our viewmodels.
/// </summary>
public partial class MainListPage : DynamicListPage
{
    private readonly IServiceProvider _serviceProvider;

    private readonly ObservableCollection<TopLevelCommandWrapper> _commands;

    private IEnumerable<IListItem>? _filteredItems;

    private bool _appsLoading = true;
    private bool _startedAppLoad;

    public MainListPage(IServiceProvider serviceProvider)
    {
        Name = "Command Palette";
        Icon = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory.ToString(), "Assets\\StoreLogo.scale-200.png"));
        ShowDetails = true;
        _serviceProvider = serviceProvider;

        var tlcManager = _serviceProvider.GetService<TopLevelCommandManager>()!;
        tlcManager.PropertyChanged += TlcManager_PropertyChanged;

        // reference the TLC collection directly... maybe? TODO is this a good idea ot a terrible one?
        _commands = tlcManager.TopLevelCommands;
        _commands.CollectionChanged += Commands_CollectionChanged;
    }

    private void TlcManager_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsLoading))
        {
            IsLoading = ActuallyLoading();
        }
    }

    private void Commands_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RaiseItemsChanged(_commands.Count);

    public override IListItem[] GetItems()
    {
        if (!_startedAppLoad)
        {
            StartAppLoad();
            _startedAppLoad = true;
        }

        return string.IsNullOrEmpty(SearchText)
            ? _commands.Select(tlc => tlc).Where(tlc => !string.IsNullOrEmpty(tlc.Title)).ToArray()
            : _filteredItems?.ToArray() ?? [];
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        /* handle changes to the filter text here */
        Debug.WriteLine($"UpdateSearchText '{oldSearch}' -> '{newSearch}'");

        if (!string.IsNullOrEmpty(SearchText))
        {
            var aliases = _serviceProvider.GetService<AliasManager>()!;
            if (aliases.CheckAlias(newSearch))
            {
                return;
            }
        }

        foreach (var command in _commands)
        {
            command.TryUpdateFallbackText(newSearch);
        }

        // Cleared out the filter text? easy. Reset _filteredItems, and bail out.
        if (string.IsNullOrEmpty(newSearch))
        {
            _filteredItems = null;
            RaiseItemsChanged(_commands.Count);
            return;
        }

        // If the new string doesn't start with the old string, then we can't
        // re-use previous results. Reset _filteredItems, and keep er moving.
        if (!newSearch.StartsWith(oldSearch, StringComparison.CurrentCultureIgnoreCase))
        {
            _filteredItems = null;
        }

        // If we don't have any previous filter results to work with, start
        // with a list of all our commands & apps.
        if (_filteredItems == null)
        {
            IEnumerable<IListItem> commands = _commands;
            IEnumerable<IListItem> apps = AllAppsCommandProvider.Page.GetItems();
            _filteredItems = commands.Concat(apps);
        }

        // Produce a list of everything that matches the current filter.
        _filteredItems = ListHelpers.FilterList<IListItem>(_filteredItems, SearchText, ScoreTopLevelItem);
        RaiseItemsChanged(_filteredItems.Count());
    }

    private bool ActuallyLoading()
    {
        var tlcManager = _serviceProvider.GetService<TopLevelCommandManager>()!;
        return _appsLoading || tlcManager.IsLoading;
    }

    private void StartAppLoad()
    {
        // This _should_ start a background thread to start loading all the apps.
        // It _feels_ like it does it's work on the main thread though, like it
        // slows down startup... which doesn't make sense.
        _ = Task.Run(() =>
        {
            _ = AppCache.Instance.Value;
            _appsLoading = false;
            IsLoading = ActuallyLoading();
        });
    }

    // Almost verbatim ListHelpers.ScoreListItem, but also accounting for the
    // fact that we want fallback handlers down-weighted, so that they don't
    // _always_ show up first.
    private static int ScoreTopLevelItem(string query, IListItem topLevelOrAppItem)
    {
        if (string.IsNullOrEmpty(query))
        {
            return 1;
        }

        var title = topLevelOrAppItem.Title;
        if (string.IsNullOrEmpty(title))
        {
            return 0;
        }

        var isFallback = false;
        if (topLevelOrAppItem is TopLevelCommandWrapper toplevel)
        {
            isFallback = toplevel.IsFallback;
        }

        var nameMatch = StringMatcher.FuzzySearch(query, title);
        var descriptionMatch = StringMatcher.FuzzySearch(query, topLevelOrAppItem.Subtitle);

        var scores = new[]
        {
            nameMatch.Score,
            (descriptionMatch.Score - 4) / 2,
            isFallback ? 1 : 0, // Always give fallbacks a chance
        };
        var max = scores.Max();
        return max / (isFallback ? 3 : 1); // but downweight them
    }
}
