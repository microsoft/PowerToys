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

    private IEnumerable<CatalogPackage>? _results;

    public static IconInfo WinGetIcon { get; } = IconHelpers.FromRelativePath("Assets\\WinGet.svg");

    public static IconInfo ExtensionsIcon { get; } = IconHelpers.FromRelativePath("Assets\\Extension.svg");

    public static string ExtensionsTag => "windows-commandpalette-extension";

    private readonly StatusMessage _errorMessage = new() { State = MessageState.Error };

    public WinGetExtensionPage(string tag = "")
    {
        Icon = tag == ExtensionsTag ? ExtensionsIcon : WinGetIcon;
        Name = Properties.Resources.winget_page_name;
        _tag = tag;
        ShowDetails = true;
    }

    public override IListItem[] GetItems()
    {
        IListItem[] items = [];
        lock (_resultsLock)
        {
            // emptySearchForTag ===
            // we don't have results yet, we haven't typed anything, and we're searching for a tag
            bool emptySearchForTag = _results == null &&
                string.IsNullOrEmpty(SearchText) &&
                HasTag;

            if (emptySearchForTag)
            {
                IsLoading = true;
                DoUpdateSearchText(string.Empty);
                return items;
            }

            if (_results != null && _results.Any())
            {
                ListItem[] results = _results.Select(PackageToListItem).ToArray();
                IsLoading = false;
                return results;
            }
        }

        EmptyContent = new CommandItem(new NoOpCommand())
        {
            Icon = WinGetIcon,
            Title = (string.IsNullOrEmpty(SearchText) && !HasTag) ?
                            Properties.Resources.winget_placeholder_text :
                            Properties.Resources.winget_no_packages_found,
        };

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
            Logger.LogDebug("Cancelling old search", memberName: nameof(DoUpdateSearchText));
            _cancellationTokenSource.Cancel();
        }

        _cancellationTokenSource = new CancellationTokenSource();

        CancellationToken cancellationToken = _cancellationTokenSource.Token;

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
            IEnumerable<CatalogPackage> results = await searchTask;

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
            Logger.LogError(ex.Message);
        }
    }

    private void UpdateWithResults(IEnumerable<CatalogPackage> results, string query)
    {
        Logger.LogDebug($"Completed search for '{query}'");
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

        string searchDebugText = $"{query}{(HasTag ? "+" : string.Empty)}{_tag}";
        Logger.LogDebug($"Starting search for '{searchDebugText}'");
        HashSet<CatalogPackage> results = new(new PackageIdCompare());

        // Default selector: this is the way to do a `winget search <query>`
        PackageMatchFilter selector = WinGetStatics.WinGetFactory.CreatePackageMatchFilter();
        selector.Field = Microsoft.Management.Deployment.PackageMatchField.CatalogDefault;
        selector.Value = query;
        selector.Option = PackageFieldMatchOption.ContainsCaseInsensitive;

        FindPackagesOptions opts = WinGetStatics.WinGetFactory.CreateFindPackagesOptions();
        opts.Selectors.Add(selector);

        // testing
        opts.ResultLimit = 25;

        // Selectors is "OR", Filters is "AND"
        if (HasTag)
        {
            PackageMatchFilter tagFilter = WinGetStatics.WinGetFactory.CreatePackageMatchFilter();
            tagFilter.Field = Microsoft.Management.Deployment.PackageMatchField.Tag;
            tagFilter.Value = _tag;
            tagFilter.Option = PackageFieldMatchOption.ContainsCaseInsensitive;

            opts.Filters.Add(tagFilter);
        }

        // Clean up here, then...
        ct.ThrowIfCancellationRequested();

        Lazy<Task<PackageCatalog>> catalogTask = HasTag ? WinGetStatics.CompositeWingetCatalog : WinGetStatics.CompositeAllCatalog;

        // Both these catalogs should have been instantiated by the
        // WinGetStatics static ctor when we were created.
        PackageCatalog catalog = await catalogTask.Value;

        if (catalog == null)
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
            Task<FindPackagesResult> internalSearchTask = Task.Run(() => catalog.FindPackages(opts), ct);
            FindPackagesResult searchResults = await internalSearchTask;

            // TODO more error handling like this:
            if (searchResults.Status != FindPackagesResultStatus.Ok)
            {
                _errorMessage.Message = string.Format(CultureInfo.CurrentCulture, ErrorMessage, searchResults.Status);
                WinGetExtensionHost.Instance.ShowStatus(_errorMessage, StatusContext.Page);
                return [];
            }

            Logger.LogDebug($"    got results for ({query})", memberName: nameof(DoSearchAsync));
            foreach (Management.Deployment.MatchResult? match in searchResults.Matches.ToArray())
            {
                ct.ThrowIfCancellationRequested();

                // Print the packages
                CatalogPackage package = match.CatalogPackage;

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
