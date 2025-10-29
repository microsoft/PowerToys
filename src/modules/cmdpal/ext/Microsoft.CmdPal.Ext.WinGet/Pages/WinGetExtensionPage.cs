// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Ext.WinGet.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Management.Deployment;

namespace Microsoft.CmdPal.Ext.WinGet;

internal sealed partial class WinGetExtensionPage : DynamicListPage, IDisposable
{
    private static readonly CompositeFormat ErrorMessage = System.Text.CompositeFormat.Parse(Properties.Resources.winget_unexpected_error);

    private readonly string _tag = string.Empty;

    public bool HasTag => !string.IsNullOrEmpty(_tag);

    private readonly Lock _resultsLock = new();
    private readonly Lock _taskLock = new();

    private string? _nextSearchQuery;
    private bool _isTaskRunning;

    private List<CatalogPackage>? _results;

    public static string ExtensionsTag => "windows-commandpalette-extension";

    private readonly StatusMessage _errorMessage = new() { State = MessageState.Error };

    public WinGetExtensionPage(string tag = "")
    {
        Icon = tag == ExtensionsTag ? Icons.ExtensionsIcon : Icons.WinGetIcon;
        Name = Properties.Resources.winget_page_name;
        _tag = tag;
        ShowDetails = true;
    }

    public override IListItem[] GetItems()
    {
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

    private static ListItem PackageToListItem(CatalogPackage p) => new InstallPackageListItem(p);

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
        HashSet<CatalogPackage> results = new(new PackageIdCompare());

        // Default selector: this is the way to do a `winget search <query>`
        var selector = WinGetStatics.WinGetFactory.CreatePackageMatchFilter();
        selector.Field = Microsoft.Management.Deployment.PackageMatchField.CatalogDefault;
        selector.Value = query;
        selector.Option = PackageFieldMatchOption.ContainsCaseInsensitive;

        var opts = WinGetStatics.WinGetFactory.CreateFindPackagesOptions();
        opts.Selectors.Add(selector);

        // testing
        opts.ResultLimit = 25;

        // Selectors is "OR", Filters is "AND"
        if (HasTag)
        {
            var tagFilter = WinGetStatics.WinGetFactory.CreatePackageMatchFilter();
            tagFilter.Field = Microsoft.Management.Deployment.PackageMatchField.Tag;
            tagFilter.Value = _tag;
            tagFilter.Option = PackageFieldMatchOption.ContainsCaseInsensitive;

            opts.Filters.Add(tagFilter);
        }

        var catalogTask = HasTag ? WinGetStatics.CompositeWingetCatalog : WinGetStatics.CompositeAllCatalog;

        // Both these catalogs should have been instantiated by the
        // WinGetStatics static ctor when we were created.
        var catalog = await catalogTask.Value;

        if (catalog is null)
        {
            // This error should have already been displayed by WinGetStatics
            return [];
        }

        // foreach (var catalog in connections)
        {
            Stopwatch findPackages_stopwatch = new();
            findPackages_stopwatch.Start();
            Logger.LogDebug($"  Searching {catalog.Info.Name} ({query})", memberName: nameof(DoSearchAsync));

            Logger.LogDebug($"Preface for \"{searchDebugText}\" took {stopwatch.ElapsedMilliseconds}ms", memberName: nameof(DoSearchAsync));

            // BODGY, re: microsoft/winget-cli#5151
            // FindPackagesAsync isn't actually async.
            var internalSearchTask = Task.Run(() => catalog.FindPackages(opts));
            var searchResults = await internalSearchTask;

            findPackages_stopwatch.Stop();
            Logger.LogDebug($"FindPackages for \"{searchDebugText}\" took {findPackages_stopwatch.ElapsedMilliseconds}ms", memberName: nameof(DoSearchAsync));

            // TODO more error handling like this:
            if (searchResults.Status != FindPackagesResultStatus.Ok)
            {
                _errorMessage.Message = string.Format(CultureInfo.CurrentCulture, ErrorMessage, searchResults.Status);
                WinGetExtensionHost.Instance.ShowStatus(_errorMessage, StatusContext.Page);
                return [];
            }

            Logger.LogDebug($"    got results for ({query})", memberName: nameof(DoSearchAsync));

            // FYI Using .ToArray or any other kind of enumerable loop
            // on arrays returned by the winget API are NOT trim safe
            var count = searchResults.Matches.Count;
            for (var i = 0; i < count; i++)
            {
                var match = searchResults.Matches[i];

                var package = match.CatalogPackage;
                results.Add(package);
            }

            Logger.LogDebug($"    ({searchDebugText}): count: {results.Count}", memberName: nameof(DoSearchAsync));
        }

        stopwatch.Stop();

        Logger.LogDebug($"Search \"{searchDebugText}\" took {stopwatch.ElapsedMilliseconds}ms", memberName: nameof(DoSearchAsync));

        return results;
    }

    public void Dispose() => throw new NotImplementedException();
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "I just like it")]
public sealed class PackageIdCompare : IEqualityComparer<CatalogPackage>
{
    public bool Equals(CatalogPackage? x, CatalogPackage? y) =>
        (x?.Id == y?.Id)
        && (x?.DefaultInstallVersion?.PackageCatalog == y?.DefaultInstallVersion?.PackageCatalog);

    public int GetHashCode([DisallowNull] CatalogPackage obj) => obj.Id.GetHashCode();
}
