// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.WinGet.Pages;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Microsoft.Management.Deployment;

namespace Microsoft.CmdPal.Ext.WinGet;

internal sealed partial class WinGetExtensionPage : DynamicListPage, IDisposable
{
    private readonly string _tag = string.Empty;

    public bool HasTag => !string.IsNullOrEmpty(_tag);

    private readonly Lock _resultsLock = new();

    private CancellationTokenSource? _cancellationTokenSource;
    private Task<IEnumerable<CatalogPackage>>? _currentSearchTask;

    private IEnumerable<CatalogPackage>? _results;

    public static IconInfo WinGetIcon { get; } = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory.ToString(), "Assets\\WinGet.png"));

    public static IconInfo ExtensionsIcon { get; } = new("\uEA86"); // Puzzle

    public static string ExtensionsTag => "windows-commandpalette-extension";

    private readonly StatusMessage _errorMessage = new() { State = MessageState.Error };

    public WinGetExtensionPage(string tag = "")
    {
        Icon = tag == ExtensionsTag ? ExtensionsIcon : WinGetIcon;
        Name = "Search Winget";
        _tag = tag;
        ShowDetails = true;
    }

    public override IListItem[] GetItems()
    {
        IListItem[] items = [];
        lock (_resultsLock)
        {
            var emptySearchForTag = _results == null &&
                string.IsNullOrEmpty(SearchText) &&
                HasTag;

            if (emptySearchForTag)
            {
                IsLoading = true;
                DoUpdateSearchText(string.Empty);
                return items;
            }

            items = (_results == null || !_results.Any())
                ? [
                    new ListItem(new NoOpCommand())
                    {
                        Title = (string.IsNullOrEmpty(SearchText) && !HasTag) ?
                            "Start typing to search for packages" :
                            "No packages found",
                    }
                ]
                : _results.Select(PackageToListItem).ToArray();
        }

        IsLoading = false;

        return items;
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
        if (_cancellationTokenSource != null)
        {
            Debug.WriteLine("Cancelling old search");
            _cancellationTokenSource.Cancel();
        }

        _cancellationTokenSource = new CancellationTokenSource();

        var cancellationToken = _cancellationTokenSource.Token;

        IsLoading = true;

        // Save the latest search task
        _currentSearchTask = DoSearchAsync(newSearch, cancellationToken);

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
            Debug.WriteLine($"  Cancelled search for '{newSearch}'");
        }
        catch (Exception ex)
        {
            // Handle other exceptions
            Console.WriteLine(ex.Message);
        }
    }

    private void UpdateWithResults(IEnumerable<CatalogPackage> results, string query)
    {
        Debug.WriteLine($"Completed search for '{query}'");
        lock (_resultsLock)
        {
            this._results = results;
        }

        RaiseItemsChanged(this._results.Count());
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
        Debug.WriteLine($"Starting search for '{searchDebugText}'");
        var results = new HashSet<CatalogPackage>(new PackageIdCompare());

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

        if (catalog == null)
        {
            // This error should have already been displayed by WinGetStatics
            return [];
        }

        // foreach (var catalog in connections)
        {
            Debug.WriteLine($"  Searching {catalog.Info.Name} ({query})");

            ct.ThrowIfCancellationRequested();

            // BODGY, re: microsoft/winget-cli#5151
            // FindPackagesAsync isn't actually async.
            var internalSearchTask = Task.Run(() => catalog.FindPackages(opts), ct);
            var searchResults = await internalSearchTask;

            // TODO more error handling like this:
            if (searchResults.Status != FindPackagesResultStatus.Ok)
            {
                _errorMessage.Message = $"Unexpected error: {searchResults.Status}";
                WinGetExtensionHost.Instance.ShowStatus(_errorMessage);
                return [];
            }

            Debug.WriteLine($"    got results for ({query})");
            foreach (var match in searchResults.Matches.ToArray())
            {
                ct.ThrowIfCancellationRequested();

                // Print the packages
                var package = match.CatalogPackage;

                results.Add(package);
            }

            Debug.WriteLine($"    ({searchDebugText}): count: {results.Count}");
        }

        stopwatch.Stop();

        Debug.WriteLine($"Search \"{searchDebugText}\" took {stopwatch.ElapsedMilliseconds}ms");

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
