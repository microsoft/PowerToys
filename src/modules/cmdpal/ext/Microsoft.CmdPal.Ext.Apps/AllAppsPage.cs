// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps;

public sealed partial class AllAppsPage : DynamicListPage, IDisposable
{
    private const double MinimumSearchScoreRatio = 0.75;
    private const int MetadataScoreOffset = 4;
    private const int MetadataScoreDivisor = 2;
    private const CompareOptions TitleCompareOptions = CompareOptions.IgnoreCase | CompareOptions.NumericOrdering;
    private static readonly CompareInfo TitleCompareInfo = CultureInfo.CurrentCulture.CompareInfo;

    private readonly IAppListItemSource _appListItemSource;
    private readonly AllAppsFilters _filters;
    private readonly ListItem _refreshingBanner = new(new NoOpCommand())
    {
        Title = Resources.refreshing_app_list,
        Icon = Icons.Reloading,
    };

    private readonly Separator _allAppsSeparator = new(Resources.all_apps);
    private readonly ListItem _noAppsPlaceholder = new(new NoOpCommand())
    {
        Title = Resources.no_apps_found,
        Icon = Icons.AllAppsIcon,
    };

    private InterlockedBoolean _disposed;

    public AllAppsPage(IAppListItemSource appListItemSource)
    {
        _appListItemSource = appListItemSource ?? throw new ArgumentNullException(nameof(appListItemSource));
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

    public IContextItem[] MoreCommands { get; }

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

        var result = new IListItem[appItems.Length + 2];
        result[0] = _refreshingBanner;
        result[1] = _allAppsSeparator;
        appItems.CopyTo(result, 2);
        return result;
    }

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

    private AppListItem[] GetFilteredAppItems()
    {
        var filterId = _filters.CurrentFilterId;
        var snapshot = _appListItemSource.GetSnapshot();
        var candidates = filterId == AllAppsFilters.HiddenFilterId
            ? snapshot.HiddenItems
            : snapshot.VisibleItems;

        var query = SearchText.Trim();
        if (string.IsNullOrWhiteSpace(query))
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

        var idealScore = FuzzyStringMatcher.ScoreFuzzy(query, query);
        var minimumMatchScore = (int)Math.Ceiling(idealScore * MinimumSearchScoreRatio);
        var scoredItems = new List<ScoredAppListItem>(candidates.Count);
        foreach (var candidate in candidates)
        {
            if (!MatchesTypeFilter(candidate, filterId))
            {
                continue;
            }

            var score = ScoreApp(query, candidate, minimumMatchScore);
            if (score > 0)
            {
                scoredItems.Add(new ScoredAppListItem(candidate, score));
            }
        }

        scoredItems.Sort(static (left, right) =>
        {
            var scoreComparison = right.Score.CompareTo(left.Score);
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
            _ => true,
        };
    }

    private static int ScoreApp(string query, AppListItem item, int minimumMatchScore)
    {
        var titleScore = FuzzyStringMatcher.ScoreFuzzy(query, item.Title);
        var subtitleScore = FuzzyStringMatcher.ScoreFuzzy(query, item.App.Subtitle);
        var hasValidMatch = titleScore >= minimumMatchScore || subtitleScore >= minimumMatchScore;

        var score = titleScore;
        score = Math.Max(score, (subtitleScore - MetadataScoreOffset) / MetadataScoreDivisor);
        foreach (var matchTerm in item.SearchTerms)
        {
            score = Math.Max(score, ScoreMetadata(query, matchTerm, minimumMatchScore, ref hasValidMatch));
        }

        return hasValidMatch ? score : 0;
    }

    private static int ScoreMetadata(string query, string? value, int minimumMatchScore, ref bool hasValidMatch)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var rawScore = FuzzyStringMatcher.ScoreFuzzy(query, value);
        hasValidMatch |= rawScore >= minimumMatchScore;
        return (rawScore - MetadataScoreOffset) / MetadataScoreDivisor;
    }

    private static string GetTitle(bool isRefreshing)
        => isRefreshing ? $"{Resources.all_apps} ({Resources.refreshing_page_title_suffix})" : Resources.all_apps;

    /// <summary>Associates an application list item with its page-search score.</summary>
    private readonly record struct ScoredAppListItem(AppListItem Item, int Score);

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
}
