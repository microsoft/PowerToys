// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.Apps.AppList;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps.Pages;

public sealed partial class AllAppsPage : DynamicListPage, IDisposable
{
    private const CompareOptions TitleCompareOptions = CompareOptions.IgnoreCase | CompareOptions.NumericOrdering;
    private static readonly CompareInfo TitleCompareInfo = CultureInfo.CurrentCulture.CompareInfo;

    private readonly IAppListItemSource _appListItemSource;
    private readonly IFuzzyMatcherProvider _fuzzyMatcherProvider;
    private readonly AllAppsFilters _filters;
    private readonly ListItem _refreshingBanner = new(new NoOpCommand())
    {
        Title = Resources.refreshing_app_list,
        Icon = Icons.Reloading,
    };

    private readonly Separator _allAppsSeparator = new(Resources.all_apps);
    private readonly Separator _manuallyHiddenSeparator = new(Resources.hidden_manually);
    private readonly Separator _patternHiddenSeparator = new(Resources.hidden_by_exclusion_patterns);
    private readonly ListItem _noAppsPlaceholder = new(new NoOpCommand())
    {
        Title = Resources.no_apps_found,
        Icon = Icons.AllAppsIcon,
    };

    private InterlockedBoolean _disposed;

    public IContextItem[] MoreCommands { get; }

    /// <summary>Initializes a new instance of the <see cref="AllAppsPage"/> class. Creates the searchable application page and subscribes to catalog and filter changes.</summary>
    public AllAppsPage(IAppListItemSource appListItemSource, IFuzzyMatcherProvider fuzzyMatcherProvider)
    {
        ArgumentNullException.ThrowIfNull(appListItemSource);
        ArgumentNullException.ThrowIfNull(fuzzyMatcherProvider);

        _appListItemSource = appListItemSource;
        _fuzzyMatcherProvider = fuzzyMatcherProvider;
        _appListItemSource.Changed += OnAppListItemSourceChanged;
        var isSourceLoading = _appListItemSource.IsLoading;
        Name = Resources.all_apps;
        Title = GetTitle(isSourceLoading);
        Icon = Icons.AllAppsIcon;
        ShowDetails = true;
        IsLoading = isSourceLoading;
        PlaceholderText = Resources.search_installed_apps_placeholder;

        _filters = new AllAppsFilters();
        _filters.PropChanged += OnFiltersChanged;
        Filters = _filters;

        MoreCommands =
        [
            new CommandContextItem(
                new AnonymousCommand(() => _ = _appListItemSource.RefreshAsync())
                {
                    Name = Resources.refresh_app_list,
                    Icon = Icons.Refresh,
                    Result = CommandResult.KeepOpen(),
                }),
        ];
    }

    public override IListItem[] GetItems()
    {
        var appItems = GetFilteredAppItems();
        if (!_appListItemSource.IsLoading)
        {
            return appItems.Length == 0 ? [_noAppsPlaceholder] : appItems;
        }

        if (appItems.Length == 0)
        {
            return [_refreshingBanner];
        }

        return _filters.CurrentFilterId == AllAppsFilters.HiddenFilterId
            ? [_refreshingBanner, .. appItems]
            : [_refreshingBanner, _allAppsSeparator, .. appItems];
    }

    /// <inheritdoc />
    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (!_disposed.Value && !string.Equals(oldSearch, newSearch, StringComparison.Ordinal))
        {
            RaiseItemsChanged(0);
        }
    }

    private void OnFiltersChanged(object sender, IPropChangedEventArgs args)
    {
        if (!_disposed.Value && args.PropertyName == nameof(Filters.CurrentFilterId))
        {
            RaiseItemsChanged(0);
        }
    }

    private void OnAppListItemSourceChanged(object? sender, EventArgs args)
    {
        if (_disposed.Value)
        {
            return;
        }

        var isSourceLoading = _appListItemSource.IsLoading;
        IsLoading = isSourceLoading;
        Title = GetTitle(isSourceLoading);
        RaiseItemsChanged();
    }

    private IListItem[] GetFilteredAppItems()
    {
        var filterId = _filters.CurrentFilterId;
        var snapshot = _appListItemSource.GetSnapshot();
        var query = SearchText;
        AppSearch? search = null;
        if (!string.IsNullOrWhiteSpace(query))
        {
            _appListItemSource.RequestExecutionAliasRefresh();
            search = new AppSearch(query, _fuzzyMatcherProvider.Current, snapshot.ExecutableNameMatchMode, snapshot.GetExecutionAliasOwner(query));
        }

        if (filterId != AllAppsFilters.HiddenFilterId)
        {
            return FilterAppItems(snapshot.VisibleItems, filterId, search);
        }

        var manuallyHidden = FilterAppItems(snapshot.HiddenItems, filterId, search);
        var patternHidden = FilterAppItems(snapshot.PatternHiddenItems, filterId, search);
        List<IListItem> items = [];
        if (manuallyHidden.Length > 0)
        {
            items.Add(_manuallyHiddenSeparator);
            items.AddRange(manuallyHidden);
        }

        if (patternHidden.Length > 0)
        {
            items.Add(_patternHiddenSeparator);
            items.AddRange(patternHidden);
        }

        return [.. items];
    }

    /// <summary>Filters applications by type and, when supplied, ranks matches using the captured search policy.</summary>
    /// <returns>Matching rows in search order, or their original order when no search is supplied.</returns>
    internal static AppListItem[] FilterAppItems(IReadOnlyList<AppListItem> candidates, string filterId, AppSearch? search)
    {
        if (search is null)
        {
            var matches = new List<AppListItem>(candidates.Count);
            foreach (var candidate in candidates)
            {
                if (MatchesTypeFilter(candidate, filterId))
                {
                    matches.Add(candidate);
                }
            }

            return [.. matches];
        }

        var scoredItems = new List<ScoredAppListItem>(candidates.Count);
        foreach (var candidate in candidates)
        {
            if (!MatchesTypeFilter(candidate, filterId))
            {
                continue;
            }

            var match = search.Evaluate(candidate);
            if (match.HasMatch)
            {
                scoredItems.Add(new ScoredAppListItem(candidate, match));
            }
        }

        scoredItems.Sort(static (left, right) =>
        {
            var titleComparison = right.Match.IsExactTitleMatch.CompareTo(left.Match.IsExactTitleMatch);
            if (titleComparison != 0)
            {
                return titleComparison;
            }

            var aliasComparison = right.Match.IsPreferredExecutionAliasMatch.CompareTo(left.Match.IsPreferredExecutionAliasMatch);
            if (aliasComparison != 0)
            {
                return aliasComparison;
            }

            var executableComparison = right.Match.IsExactExecutableMatch.CompareTo(left.Match.IsExactExecutableMatch);
            if (executableComparison != 0)
            {
                return executableComparison;
            }

            var scoreComparison = right.Match.LexicalScore.CompareTo(left.Match.LexicalScore);
            return scoreComparison != 0
                ? scoreComparison
                : TitleCompareInfo.Compare(left.Item.Title, right.Item.Title, TitleCompareOptions);
        });

        var results = new AppListItem[scoredItems.Count];
        for (var i = 0; i < scoredItems.Count; i++)
        {
            results[i] = scoredItems[i].Item;
        }

        return results;
    }

    private static bool MatchesTypeFilter(AppListItem item, string filterId)
    {
        return filterId switch
        {
            AllAppsFilters.Win32FilterId => !item.App.IsPackaged,
            AllAppsFilters.PackagedFilterId => item.App.IsPackaged,
            AllAppsFilters.WebFilterId => item.App.IsWebApp,
            _ => true,
        };
    }

    private static string GetTitle(bool isRefreshing)
    {
        return isRefreshing ? $"{Resources.all_apps} ({Resources.refreshing_page_title_suffix})" : Resources.all_apps;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed.Set())
        {
            return;
        }

        _appListItemSource.Changed -= OnAppListItemSourceChanged;
        _filters.PropChanged -= OnFiltersChanged;
        GC.SuppressFinalize(this);
    }

    /// <summary>Associates an application list item with its page-search score.</summary>
    private readonly record struct ScoredAppListItem(AppListItem Item, AppSearch.Match Match);
}
