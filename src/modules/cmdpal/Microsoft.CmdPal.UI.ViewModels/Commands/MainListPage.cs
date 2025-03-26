// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Specialized;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Ext.Apps;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.CmdPal.UI.ViewModels.MainPage;

/// <summary>
/// This class encapsulates the data we load from built-in providers and extensions to use within the same extension-UI system for a <see cref="ListPage"/>.
/// TODO: Need to think about how we structure/interop for the page -> section -> item between the main setup, the extensions, and our viewmodels.
/// </summary>
public partial class MainListPage : DynamicListPage,
    IRecipient<ClearSearchMessage>,
    IRecipient<UpdateFallbackItemsMessage>
{
    private readonly IServiceProvider _serviceProvider;

    private readonly TopLevelCommandManager _tlcManager;
    private IEnumerable<IListItem>? _filteredItems;

    public MainListPage(IServiceProvider serviceProvider)
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.scale-200.png");
        _serviceProvider = serviceProvider;

        _tlcManager = _serviceProvider.GetService<TopLevelCommandManager>()!;
        _tlcManager.PropertyChanged += TlcManager_PropertyChanged;
        _tlcManager.TopLevelCommands.CollectionChanged += Commands_CollectionChanged;

        // The all apps page will kick off a BG thread to start loading apps.
        // We just want to know when it is done.
        var allApps = AllAppsCommandProvider.Page;
        allApps.PropChanged += (s, p) =>
        {
            if (p.PropertyName == nameof(allApps.IsLoading))
            {
                IsLoading = ActuallyLoading();
            }
        };

        WeakReferenceMessenger.Default.Register<ClearSearchMessage>(this);
        WeakReferenceMessenger.Default.Register<UpdateFallbackItemsMessage>(this);

        var settings = _serviceProvider.GetService<SettingsModel>()!;
        settings.SettingsChanged += SettingsChangedHandler;
        HotReloadSettings(settings);

        IsLoading = true;
    }

    private void TlcManager_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsLoading))
        {
            IsLoading = ActuallyLoading();
        }
    }

    private void Commands_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RaiseItemsChanged(_tlcManager.TopLevelCommands.Count);

    public override IListItem[] GetItems()
    {
        if (string.IsNullOrEmpty(SearchText))
        {
            lock (_tlcManager.TopLevelCommands)
            {
                return _tlcManager
                    .TopLevelCommands
                    .Where(tlc => !string.IsNullOrEmpty(tlc.Title))
                    .ToArray();
            }
        }
        else
        {
            lock (_tlcManager.TopLevelCommands)
            {
                return _filteredItems?.ToArray() ?? [];
            }
        }
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        // Handle changes to the filter text here
        if (!string.IsNullOrEmpty(SearchText))
        {
            var aliases = _serviceProvider.GetService<AliasManager>()!;
            if (aliases.CheckAlias(newSearch))
            {
                return;
            }
        }

        var commands = _tlcManager.TopLevelCommands;
        lock (commands)
        {
            // This gets called on a background thread, because ListViewModel
            // updates the .SearchText of all extensions on a BG thread.
            foreach (var command in commands)
            {
                command.TryUpdateFallbackText(newSearch);
            }

            // Cleared out the filter text? easy. Reset _filteredItems, and bail out.
            if (string.IsNullOrEmpty(newSearch))
            {
                _filteredItems = null;
                RaiseItemsChanged(commands.Count);
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
                IEnumerable<IListItem> apps = AllAppsCommandProvider.Page.GetItems();
                _filteredItems = commands.Concat(apps);
            }

            // Produce a list of everything that matches the current filter.
            _filteredItems = ListHelpers.FilterList<IListItem>(_filteredItems, SearchText, ScoreTopLevelItem);
            RaiseItemsChanged(_filteredItems.Count());
        }
    }

    private bool ActuallyLoading()
    {
        var tlcManager = _serviceProvider.GetService<TopLevelCommandManager>()!;
        var allApps = AllAppsCommandProvider.Page;
        return allApps.IsLoading || tlcManager.IsLoading;
    }

    // Almost verbatim ListHelpers.ScoreListItem, but also accounting for the
    // fact that we want fallback handlers down-weighted, so that they don't
    // _always_ show up first.
    private int ScoreTopLevelItem(string query, IListItem topLevelOrAppItem)
    {
        var title = topLevelOrAppItem.Title;
        if (string.IsNullOrWhiteSpace(title))
        {
            return 0;
        }

        var isWhiteSpace = string.IsNullOrWhiteSpace(query);

        var isFallback = false;
        var isAliasSubstringMatch = false;
        var isAliasMatch = false;
        var id = IdForTopLevelOrAppItem(topLevelOrAppItem);

        var extensionDisplayName = string.Empty;
        if (topLevelOrAppItem is TopLevelViewModel topLevel)
        {
            isFallback = topLevel.IsFallback;
            if (topLevel.HasAlias)
            {
                var alias = topLevel.AliasText;
                isAliasMatch = alias == query;
                isAliasSubstringMatch = isAliasMatch || alias.StartsWith(query, StringComparison.CurrentCultureIgnoreCase);
            }

            extensionDisplayName = topLevel.ExtensionHost?.Extension?.PackageDisplayName ?? string.Empty;
        }

        // StringMatcher.FuzzySearch will absolutely BEEF IT if you give it a
        // whitespace-only query.
        //
        // in that scenario, we'll just use a simple string contains for the
        // query. Maybe someone is really looking for things with a space in
        // them, I don't know.

        // Title:
        // * whitespace query: 1 point
        // * otherwise full weight match
        var nameMatch = isWhiteSpace ?
            (title.Contains(query) ? 1 : 0) :
            StringMatcher.FuzzySearch(query, title).Score;

        // Subtitle:
        // * whitespace query: 1/2 point
        // * otherwise ~half weight match. Minus a bit, because subtitles tend to be longer
        var descriptionMatch = isWhiteSpace ?
            (topLevelOrAppItem.Subtitle.Contains(query) ? .5 : 0) :
            (StringMatcher.FuzzySearch(query, topLevelOrAppItem.Subtitle).Score - 4) / 2.0;

        // Extension title: despite not being visible, give the extension name itself some weight
        // * whitespace query: 0 points
        // * otherwise more weight than a subtitle, but not much
        var extensionTitleMatch = isWhiteSpace ? 0 : StringMatcher.FuzzySearch(query, extensionDisplayName).Score / 1.5;

        var scores = new[]
        {
             nameMatch,
             descriptionMatch,
             isFallback ? 1 : 0, // Always give fallbacks a chance...
        };
        var max = scores.Max();

        // _Add_ the extension name. This will bubble items that match both
        // title and extension name up above ones that just match title.
        // e.g. "git" will up-weight "GitHub searches" from the GitHub extension
        // above "git" from "whatever"
        max = max + extensionTitleMatch;

        // ... but downweight them
        var matchSomething = (max / (isFallback ? 3 : 1))
            + (isAliasMatch ? 9001 : (isAliasSubstringMatch ? 1 : 0));

        // If we matched title, subtitle, or alias (something real), then
        // here we add the recent command weight boost
        //
        // Otherwise something like `x` will still match everything you've run before
        var finalScore = matchSomething;
        if (matchSomething > 0)
        {
            var history = _serviceProvider.GetService<AppStateModel>()!.RecentCommands;
            var recentWeightBoost = history.GetCommandHistoryWeight(id);
            finalScore += recentWeightBoost;
        }

        return (int)finalScore;
    }

    public void UpdateHistory(IListItem topLevelOrAppItem)
    {
        var id = IdForTopLevelOrAppItem(topLevelOrAppItem);
        var state = _serviceProvider.GetService<AppStateModel>()!;
        var history = state.RecentCommands;
        history.AddHistoryItem(id);
        AppStateModel.SaveState(state);
    }

    private string IdForTopLevelOrAppItem(IListItem topLevelOrAppItem)
    {
        if (topLevelOrAppItem is TopLevelViewModel topLevel)
        {
            return topLevel.Id;
        }
        else
        {
            // we've got an app here
            return topLevelOrAppItem.Command?.Id ?? string.Empty;
        }
    }

    public void Receive(ClearSearchMessage message) => SearchText = string.Empty;

    public void Receive(UpdateFallbackItemsMessage message) => RaiseItemsChanged(_tlcManager.TopLevelCommands.Count);

    private void SettingsChangedHandler(SettingsModel sender, object? args) => HotReloadSettings(sender);

    private void HotReloadSettings(SettingsModel settings) => ShowDetails = settings.ShowAppDetails;
}
