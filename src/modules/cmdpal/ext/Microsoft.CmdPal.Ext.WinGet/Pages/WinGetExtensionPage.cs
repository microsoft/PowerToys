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

    private CancellationTokenSource? _cancellationTokenSource;
    private Task<IEnumerable<CatalogPackage>>? _currentSearchTask;

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

                IsLoading = false;
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

        IsLoading = false;

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
        // Cancel any ongoing search
        if (_cancellationTokenSource is not null)
        {
            Logger.LogDebug("Cancelling old search", memberName: nameof(DoUpdateSearchText));
            _cancellationTokenSource.Cancel();
        }

        _cancellationTokenSource = new CancellationTokenSource();

        var cancellationToken = _cancellationTokenSource.Token;

        IsLoading = true;

        try
        {
            // Save the latest search task
            _currentSearchTask = DoSearchAsync(newSearch, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // DO NOTHING HERE
            return;
        }
        catch (Exception ex)
        {
            // Handle other exceptions
            ExtensionHost.LogMessage($"[WinGet] DoUpdateSearchText throw exception: {ex.Message}");
            return;
        }

        // Await the task to ensure only the latest one gets processed
        _ = ProcessSearchResultsAsync(_currentSearchTask, newSearch);
    }

    private async Task ProcessSearchResultsAsync(
        Task<IEnumerable<CatalogPackage>> searchTask,
        string newSearch)
    {
        try
        {
            var results = await searchTask;

            // Ensure this is still the latest task
            if (_currentSearchTask == searchTask)
            {
                // Process the results (e.g., update UI)
                UpdateWithResults(results, newSearch);
            }
        }
        catch (OperationCanceledException)
        {
            // Handle cancellation gracefully (e.g., log or ignore)
            Logger.LogDebug($"  Cancelled search for '{newSearch}'");
        }
        catch (Exception ex)
        {
            // Handle other exceptions
            Logger.LogError("Unexpected error while processing results", ex);
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

    private async Task<IEnumerable<CatalogPackage>> DoSearchAsync(string query, CancellationToken ct)
    {
        // Were we already canceled?
        ct.ThrowIfCancellationRequested();

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

        // Clean up here, then...
        ct.ThrowIfCancellationRequested();

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
            Logger.LogDebug($"  Searching {catalog.Info.Name} ({query})", memberName: nameof(DoSearchAsync));

            ct.ThrowIfCancellationRequested();

            // BODGY, re: microsoft/winget-cli#5151
            // FindPackagesAsync isn't actually async.
            var internalSearchTask = Task.Run(() => catalog.FindPackages(opts), ct);
            var searchResults = await internalSearchTask;

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

                ct.ThrowIfCancellationRequested();

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
