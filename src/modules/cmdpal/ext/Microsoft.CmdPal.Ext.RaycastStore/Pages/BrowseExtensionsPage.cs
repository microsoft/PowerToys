// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Ext.RaycastStore.GitHub;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.RaycastStore.Pages;

internal sealed partial class BrowseExtensionsPage : DynamicListPage
{
    private readonly RaycastGitHubClient _client;
    private readonly InstalledExtensionTracker? _tracker;
    private readonly Lock _taskLock = new();
    private readonly Lock _resultsLock = new();

    private readonly StatusMessage _rateLimitMessage = new()
    {
        State = MessageState.Warning,
        Message = "GitHub API rate limit reached. Set GITHUB_TOKEN environment variable for higher limits (5000/hr vs 60/hr).",
    };

    private string? _nextSearchQuery;
    private bool _isTaskRunning;
    private List<RaycastExtensionInfo>? _searchResults;

    public BrowseExtensionsPage(RaycastGitHubClient client, InstalledExtensionTracker? tracker = null)
    {
        _client = client;
        _tracker = tracker;
        Icon = Icons.RaycastIcon;
        Name = "Browse Raycast Extensions";
        Title = "Browse Raycast Extensions";
        PlaceholderText = "Search extensions by name (e.g. github, todoist, spotify)...";
        ShowDetails = true;
        GridProperties = new MediumGridLayout
        {
            ShowTitle = true,
        };
        EmptyContent = new CommandItem(new NoOpCommand())
        {
            Icon = Icons.SearchIcon,
            Title = "Search for Raycast extensions",
            Subtitle = "Type a name to search the Raycast extension catalog",
        };
    }

    public override IListItem[] GetItems()
    {
        using (_resultsLock.EnterScope())
        {
            if (_searchResults != null && _searchResults.Count > 0)
            {
                IListItem[] array = new IListItem[_searchResults.Count];
                for (var i = 0; i < _searchResults.Count; i++)
                {
                    array[i] = new ExtensionListItem(_searchResults[i], _tracker);
                }

                return array;
            }
        }

        if (!string.IsNullOrEmpty(SearchText))
        {
            var subtitle = _client.IsRateLimited()
                ? "GitHub API rate limit reached. Set GITHUB_TOKEN for higher limits."
                : "No Raycast extensions match \"" + SearchText + "\"";
            EmptyContent = new CommandItem(new NoOpCommand())
            {
                Icon = Icons.SearchIcon,
                Title = "No extensions found",
                Subtitle = subtitle,
            };
        }
        else
        {
            EmptyContent = new CommandItem(new NoOpCommand())
            {
                Icon = Icons.SearchIcon,
                Title = "Search for Raycast extensions",
                Subtitle = "Type a name to search the Raycast extension catalog",
            };
        }

        return Array.Empty<IListItem>();
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (newSearch != oldSearch)
        {
            DoUpdateSearchText(newSearch);
        }
    }

    private void DoUpdateSearchText(string newSearch)
    {
        using (_taskLock.EnterScope())
        {
            if (_isTaskRunning)
            {
                _nextSearchQuery = newSearch;
                return;
            }

            _isTaskRunning = true;
            _nextSearchQuery = null;

            if (string.IsNullOrWhiteSpace(newSearch))
            {
                using (_resultsLock.EnterScope())
                {
                    _searchResults = null;
                }

                _isTaskRunning = false;
                IsLoading = false;
                RaiseItemsChanged();
            }
            else
            {
                IsLoading = true;
                _ = ExecuteSearchChainAsync(newSearch);
            }
        }
    }

    private async Task ExecuteSearchChainAsync(string query)
    {
        while (true)
        {
            try
            {
                await SearchExtensionsAsync(query);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error searching extensions for '" + query + "': " + ex.Message);
            }

            string? nextQuery;
            using (_taskLock.EnterScope())
            {
                if (_nextSearchQuery == null)
                {
                    _isTaskRunning = false;
                    IsLoading = false;
                    return;
                }

                nextQuery = _nextSearchQuery;
                _nextSearchQuery = null;
            }

            query = nextQuery;
            if (string.IsNullOrWhiteSpace(query))
            {
                break;
            }

            await Task.Yield();
        }

        using (_resultsLock.EnterScope())
        {
            _searchResults = null;
        }

        using (_taskLock.EnterScope())
        {
            _isTaskRunning = false;
        }

        IsLoading = false;
        RaiseItemsChanged();
    }

    private async Task SearchExtensionsAsync(string query)
    {
        if (_client.IsRateLimited())
        {
            Logger.LogWarning("Rate limited, skipping search");
            RaycastStoreExtensionHost.Instance.ShowStatus(_rateLimitMessage, StatusContext.Page);
            return;
        }

        List<RaycastExtensionInfo> results = await _client.SearchExtensionsAsync(query);
        using (_resultsLock.EnterScope())
        {
            _searchResults = results;
        }

        RaiseItemsChanged();
    }
}
