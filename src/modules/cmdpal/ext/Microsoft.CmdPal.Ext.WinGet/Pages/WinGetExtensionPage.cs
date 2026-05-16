// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Common.WinGet;
using Microsoft.CmdPal.Common.WinGet.Services;
using Microsoft.CmdPal.Ext.WinGet.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Management.Deployment;

namespace Microsoft.CmdPal.Ext.WinGet;

internal sealed partial class WinGetExtensionPage : DynamicListPage, IDisposable
{
    private static readonly CompositeFormat ErrorMessage = CompositeFormat.Parse(Properties.Resources.winget_unexpected_error);

    private readonly string _tag = string.Empty;
    private readonly IWinGetPackageManagerService _winGetPackageManagerService;
    private readonly IWinGetOperationTrackerService _winGetOperationTrackerService;
    private readonly TaskScheduler _uiScheduler;

    public bool HasTag => !string.IsNullOrEmpty(_tag);

    private readonly Lock _resultsLock = new();
    private readonly Lock _taskLock = new();

    private string? _nextSearchQuery;
    private bool _isTaskRunning;

    private List<CatalogPackage>? _results;

    public static string ExtensionsTag => WinGetPackageTags.CommandPaletteExtension;

    private readonly StatusMessage _errorMessage = new() { State = MessageState.Error };

    public WinGetExtensionPage(
        IWinGetPackageManagerService winGetPackageManagerService,
        IWinGetOperationTrackerService winGetOperationTrackerService,
        TaskScheduler uiScheduler,
        string tag = "")
    {
        _winGetPackageManagerService = winGetPackageManagerService;
        _winGetOperationTrackerService = winGetOperationTrackerService;
        _uiScheduler = uiScheduler;
        Icon = tag == ExtensionsTag ? Icons.ExtensionsIcon : Icons.WinGetIcon;
        Id = tag == ExtensionsTag ? "com.microsoft.cmdpal.winget-extensions" : "com.microsoft.cmdpal.winget";
        Name = Properties.Resources.winget_page_name;
        _tag = tag;
        ShowDetails = true;
    }

    public override IListItem[] GetItems()
    {
        if (!_winGetPackageManagerService.State.IsAvailable)
        {
            IsLoading = false;
            EmptyContent = new CommandItem(new NoOpCommand())
            {
                Icon = Icons.WinGetIcon,
                Title = _winGetPackageManagerService.State.Message ?? Properties.Resources.winget_no_packages_found,
            };

            return [];
        }

        lock (_resultsLock)
        {
            // emptySearchForTag ===
            // we don't have results yet, we haven't typed anything, and we're searching for a tag
            var emptySearchForTag = _results is null &&
                string.IsNullOrEmpty(SearchText) &&
                HasTag;

            if (emptySearchForTag)
            {
                IsLoading = true;
                DoUpdateSearchText(string.Empty);
                return [];
            }

            if (_results is not null && _results.Count != 0)
            {
                var stopwatch = Stopwatch.StartNew();
                var count = _results.Count;
                var results = new ListItem[count];
                var next = 0;
                for (var i = 0; i < count; i++)
                {
                    try
                    {
                        var li = PackageToListItem(_results[i]);
                        results[next] = li;
                        next++;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("error converting result to listitem", ex);
                    }
                }

                stopwatch.Stop();
                Logger.LogDebug($"Building ListItems took {stopwatch.ElapsedMilliseconds}ms", memberName: nameof(GetItems));
                return results;
            }
        }

        EmptyContent = new CommandItem(new NoOpCommand())
        {
            Icon = Icons.WinGetIcon,
            Title = (string.IsNullOrEmpty(SearchText) && !HasTag) ?
                            Properties.Resources.winget_placeholder_text :
                            Properties.Resources.winget_no_packages_found,
        };

        return [];
    }

    private ListItem PackageToListItem(CatalogPackage p) => new InstallPackageListItem(p, _winGetPackageManagerService, _winGetOperationTrackerService, _uiScheduler);

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (newSearch == oldSearch)
        {
            return;
        }

        DoUpdateSearchText(newSearch);
    }

    private void DoUpdateSearchText(string newSearch)
    {
        lock (_taskLock)
        {
            if (_isTaskRunning)
            {
                // If a task is running, queue the next search query
                // Keep IsLoading = true since we still have work to do
                Logger.LogDebug($"Task is running, queueing next search: '{newSearch}'", memberName: nameof(DoUpdateSearchText));
                _nextSearchQuery = newSearch;
            }
            else
            {
                // No task is running, start a new search
                Logger.LogDebug($"Starting new search: '{newSearch}'", memberName: nameof(DoUpdateSearchText));
                _isTaskRunning = true;
                _nextSearchQuery = null;
                IsLoading = true;

                _ = ExecuteSearchChainAsync(newSearch);
            }
        }
    }

    private async Task ExecuteSearchChainAsync(string query)
    {
        try
        {
            while (true)
            {
                try
                {
                    Logger.LogDebug($"Executing search for '{query}'", memberName: nameof(ExecuteSearchChainAsync));

                    var results = await DoSearchAsync(query);

                    // Update UI with results
                    UpdateWithResults(results, query);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Unexpected error while searching for '{query}'", ex);
                }

                // Check if there's a next query to process
                string? nextQuery;
                lock (_taskLock)
                {
                    if (_nextSearchQuery is not null)
                    {
                        // There's a queued search, execute it
                        nextQuery = _nextSearchQuery;
                        _nextSearchQuery = null;

                        Logger.LogDebug($"Found queued search, continuing with: '{nextQuery}'", memberName: nameof(ExecuteSearchChainAsync));
                    }
                    else
                    {
                        // No more searches queued, mark task as completed
                        _isTaskRunning = false;
                        IsLoading = false;
                        Logger.LogDebug("No more queued searches, task chain completed", memberName: nameof(ExecuteSearchChainAsync));
                        break;
                    }
                }

                // Continue with the next query
                query = nextQuery;
            }
        }
        finally
        {
            lock (_taskLock)
            {
                _isTaskRunning = false;
                IsLoading = false;
            }
        }
    }

    private void UpdateWithResults(IEnumerable<CatalogPackage> results, string query)
    {
        Logger.LogDebug($"Completed search for '{query}'");
        lock (_resultsLock)
        {
            this._results = results.ToList();
        }

        RaiseItemsChanged();
    }

    private async Task<IEnumerable<CatalogPackage>> DoSearchAsync(string query)
    {
        Stopwatch stopwatch = new();
        stopwatch.Start();

        if (string.IsNullOrEmpty(query)
            && string.IsNullOrEmpty(_tag))
        {
            return [];
        }

        var searchDebugText = $"{query}{(HasTag ? "+" : string.Empty)}{_tag}";
        Logger.LogDebug($"Starting search for '{searchDebugText}'");
        var searchResult = await _winGetPackageManagerService.SearchPackagesAsync(
            query,
            tag: HasTag ? _tag : null,
            includeStoreCatalog: !HasTag,
            resultLimit: 25);

        if (!searchResult.IsSuccess)
        {
            if (!searchResult.IsUnavailable && !string.IsNullOrWhiteSpace(searchResult.ErrorMessage))
            {
                _errorMessage.Message = string.Format(CultureInfo.CurrentCulture, ErrorMessage, searchResult.ErrorMessage);
                WinGetExtensionHost.Instance.ShowStatus(_errorMessage, StatusContext.Page);
            }

            return [];
        }

        stopwatch.Stop();

        Logger.LogDebug($"Search \"{searchDebugText}\" took {stopwatch.ElapsedMilliseconds}ms", memberName: nameof(DoSearchAsync));

        return searchResult.Value ?? [];
    }

    public void Dispose()
    {
        // nothing to dispose (yet)
    }
}
