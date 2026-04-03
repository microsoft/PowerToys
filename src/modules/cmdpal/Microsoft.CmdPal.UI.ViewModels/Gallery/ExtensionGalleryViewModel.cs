// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManagedCommon;
using Microsoft.CmdPal.Common.ExtensionGallery.Models;
using Microsoft.CmdPal.Common.ExtensionGallery.Services;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Common.WinGet.Models;
using Microsoft.CmdPal.Common.WinGet.Services;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.Gallery;

public sealed partial class ExtensionGalleryViewModel : ObservableObject, IDisposable
{
    private const string WinGetSourceType = "winget";
    private static readonly TimeSpan WinGetRefreshTimeout = TimeSpan.FromSeconds(5);
    private static readonly StringComparer SortStringComparer = StringComparer.CurrentCultureIgnoreCase;
    private static readonly CompositeFormat LabelGalleryExtensionsAvailable
        = CompositeFormat.Parse(Microsoft.CmdPal.UI.ViewModels.Properties.Resources.gallery_n_extensions_available!);

    private static readonly CompositeFormat LabelGalleryExtensionsFound
        = CompositeFormat.Parse(Microsoft.CmdPal.UI.ViewModels.Properties.Resources.gallery_n_extensions_found!);

    private readonly IExtensionGalleryService _galleryService;
    private readonly IExtensionService _extensionService;
    private readonly IWinGetPackageManagerService? _winGetPackageManagerService;
    private readonly IWinGetOperationTrackerService? _winGetOperationTrackerService;
    private readonly IWinGetPackageStatusService? _winGetPackageStatusService;
    private readonly TaskScheduler _uiScheduler;
    private readonly Lock _entriesLock = new();
    private readonly List<GalleryExtensionViewModel> _allEntries = [];
    private readonly Dictionary<string, List<GalleryExtensionViewModel>> _entriesByWinGetId = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource _cts = new();
    private bool _disposed;

    public ObservableCollection<GalleryExtensionViewModel> FilteredEntries { get; } = [];

    private string _searchText = string.Empty;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }
    }

    public string ItemCounterText
    {
        get
        {
            var hasQuery = !string.IsNullOrWhiteSpace(_searchText);
            int count;
            if (hasQuery)
            {
                count = FilteredEntries.Count;
            }
            else
            {
                lock (_entriesLock)
                {
                    count = _allEntries.Count;
                }
            }

            var format = hasQuery ? LabelGalleryExtensionsFound : LabelGalleryExtensionsAvailable;
            return string.Format(CultureInfo.CurrentCulture, format, count);
        }
    }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool FromCache { get; set; }

    [ObservableProperty]
    public partial bool UsedFallbackCache { get; set; }

    [ObservableProperty]
    public partial ExtensionGallerySortOption SelectedSortOption { get; set; } = ExtensionGallerySortOption.Featured;

    public bool ShowNoResultsPanel => !string.IsNullOrWhiteSpace(_searchText) && FilteredEntries.Count == 0;

    public bool HasResults => !IsLoading && !ShowNoResultsPanel && FilteredEntries.Count > 0;

    public bool IsCustomFeed => _galleryService.IsCustomFeed;

    public string CustomFeedUrl => _galleryService.GetBaseUrl();

    public bool IsSortByFeaturedSelected => SelectedSortOption == ExtensionGallerySortOption.Featured;

    public bool IsSortByNameSelected => SelectedSortOption == ExtensionGallerySortOption.Name;

    public bool IsSortByAuthorSelected => SelectedSortOption == ExtensionGallerySortOption.Author;

    public bool IsSortByInstallationStatusSelected => SelectedSortOption == ExtensionGallerySortOption.InstallationStatus;

    public ExtensionGalleryViewModel(
        IExtensionGalleryService galleryService,
        IExtensionService extensionService,
        IWinGetPackageManagerService? winGetPackageManagerService = null,
        IWinGetPackageStatusService? winGetPackageStatusService = null,
        IWinGetOperationTrackerService? winGetOperationTrackerService = null,
        TaskScheduler? uiScheduler = null)
    {
        _galleryService = galleryService;
        _extensionService = extensionService;
        _winGetPackageManagerService = winGetPackageManagerService;
        _winGetPackageStatusService = winGetPackageStatusService;
        _winGetOperationTrackerService = winGetOperationTrackerService;
        _uiScheduler = uiScheduler ?? TaskScheduler.Current;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);

        if (_winGetOperationTrackerService is not null)
        {
            _winGetOperationTrackerService.OperationStarted += OnWinGetOperationStarted;
            _winGetOperationTrackerService.OperationUpdated += OnWinGetOperationUpdated;
            _winGetOperationTrackerService.OperationCompleted += OnWinGetOperationCompleted;
        }
    }

    public IAsyncRelayCommand RefreshCommand { get; }

    [RelayCommand]
    private void SortByFeatured()
    {
        SelectedSortOption = ExtensionGallerySortOption.Featured;
    }

    [RelayCommand]
    private void SortByName()
    {
        SelectedSortOption = ExtensionGallerySortOption.Name;
    }

    [RelayCommand]
    private void SortByAuthor()
    {
        SelectedSortOption = ExtensionGallerySortOption.Author;
    }

    [RelayCommand]
    private void SortByInstallationStatus()
    {
        SelectedSortOption = ExtensionGallerySortOption.InstallationStatus;
    }

    public async Task LoadAsync()
    {
        await FetchCoreAsync(_galleryService.FetchExtensionsAsync, refreshInstallationStatus: false);
    }

    private async Task RefreshAsync()
    {
        await FetchCoreAsync(_galleryService.RefreshAsync, refreshInstallationStatus: true);
    }

    private async Task FetchCoreAsync(Func<CancellationToken, Task<GalleryFetchResult>> fetchFunc, bool refreshInstallationStatus)
    {
        var cts = ResetCancellation();

        IsLoading = true;
        HasError = false;
        ErrorMessage = null;
        FromCache = false;
        UsedFallbackCache = false;
        NotifyStateChanged();

        try
        {
            var result = await RunInBackgroundAsync(() => fetchFunc(cts.Token), cts.Token);
            cts.Token.ThrowIfCancellationRequested();
            ApplyEntries(result.Extensions);
            HasError = result.HasError;
            ErrorMessage = result.ErrorMessage;
            FromCache = result.FromCache;
            UsedFallbackCache = result.UsedFallbackCache;
            ApplyFilter();

            StartBackgroundRefresh(refreshInstallationStatus, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Cancelled by navigation or dispose — not an error
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    private void ApplyEntries(IReadOnlyList<GalleryExtensionEntry> entries)
    {
        lock (_entriesLock)
        {
            _allEntries.Clear();
            foreach (var entry in entries)
            {
                _allEntries.Add(CreateEntryViewModel(entry));
            }

            RebuildWinGetEntryIndex();
        }

        ApplyCurrentWinGetOperations();
    }

    private void StartBackgroundRefresh(
        bool refreshInstallationStatus,
        CancellationToken cancellationToken)
    {
        _ = CheckInstalledAsync(
            cancellationToken,
            refreshInstalledExtensions: refreshInstallationStatus,
            refreshWinGetCatalogs: refreshInstallationStatus);
    }

    private CancellationTokenSource ResetCancellation()
    {
        var oldCts = _cts;
        var newCts = new CancellationTokenSource();
        _cts = newCts;
        oldCts.Cancel();
        oldCts.Dispose();
        return newCts;
    }

    private async Task CheckInstalledAsync(
        CancellationToken cancellationToken,
        bool refreshInstalledExtensions = false,
        bool refreshWinGetCatalogs = false)
    {
        List<GalleryExtensionViewModel> snapshot;
        try
        {
            var installedExtensions = refreshInstalledExtensions
                ? await RunInBackgroundAsync(
                    () => _extensionService.RefreshInstalledExtensionsAsync(includeDisabledExtensions: true),
                    cancellationToken)
                : await RunInBackgroundAsync(
                    () => _extensionService.GetInstalledExtensionsAsync(includeDisabledExtensions: true),
                    cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var installedPfns = new HashSet<string>(
                installedExtensions
                    .Select(e => e.PackageFamilyName)
                    .Where(pfn => !string.IsNullOrEmpty(pfn)),
                StringComparer.OrdinalIgnoreCase);

            lock (_entriesLock)
            {
                snapshot = [.. _allEntries];
            }

            foreach (var entry in snapshot)
            {
                if (!string.IsNullOrEmpty(entry.PackageFamilyName))
                {
                    entry.IsInstalled = installedPfns.Contains(entry.PackageFamilyName);
                    entry.IsInstalledStateKnown = true;
                }
            }

            QueueApplyFilter();
        }
        catch (OperationCanceledException)
        {
            // Cancelled — non-critical
        }
        catch (Exception ex)
        {
            // Non-critical; leave IsInstalled as false
            Logger.LogError("Failed to check installed extensions", ex);
        }

        if (_winGetPackageStatusService is null)
        {
            return;
        }

        if (refreshWinGetCatalogs && _winGetPackageManagerService is not null && _winGetPackageManagerService.State.IsAvailable)
        {
            try
            {
                using var refreshCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                refreshCts.CancelAfter(WinGetRefreshTimeout);
                await RunInBackgroundAsync(
                    () => _winGetPackageManagerService.RefreshCatalogsAsync(refreshCts.Token),
                    refreshCts.Token);
                refreshCts.Token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to refresh WinGet catalogs", ex);
                return;
            }
        }

        try
        {
            lock (_entriesLock)
            {
                snapshot = [.. _allEntries];
            }

            var wingetIds = snapshot
                .Select(entry => entry.WinGetId)
                .Where(static id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToArray();
            if (wingetIds.Length == 0)
            {
                return;
            }

            using var wingetCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            wingetCts.CancelAfter(WinGetRefreshTimeout);
            var wingetInfos = await RunInBackgroundAsync(
                () => _winGetPackageStatusService.TryGetPackageInfosAsync(wingetIds, wingetCts.Token),
                wingetCts.Token);
            wingetCts.Token.ThrowIfCancellationRequested();
            if (wingetInfos is null)
            {
                return;
            }

            foreach (var entry in snapshot)
            {
                if (string.IsNullOrWhiteSpace(entry.WinGetId))
                {
                    continue;
                }

                if (!wingetInfos.TryGetValue(entry.WinGetId, out var packageInfo))
                {
                    continue;
                }

                entry.ApplyWinGetPackageInfo(packageInfo);
            }

            QueueApplyFilter();
        }
        catch (OperationCanceledException)
        {
            // Cancelled or timed out — non-critical.
        }
        catch (Exception ex)
        {
            // Non-critical; keep the gallery visible with its existing state.
            Logger.LogError("Failed to check WinGet package status", ex);
        }
    }

    private GalleryExtensionViewModel CreateEntryViewModel(GalleryExtensionEntry entry)
    {
        return new GalleryExtensionViewModel(
            entry,
            _galleryService,
            _winGetPackageManagerService,
            _winGetPackageStatusService,
            _winGetOperationTrackerService);
    }

    private void ApplyFilter()
    {
        List<GalleryExtensionViewModel> snapshot;
        lock (_entriesLock)
        {
            snapshot = [.. _allEntries];
        }

        var filtered = ListHelpers.FilterList(snapshot, _searchText, Matches).ToList();
        SortEntries(filtered);
        ListHelpers.InPlaceUpdateList(FilteredEntries, filtered);
        NotifyStateChanged();
    }

    private void SortEntries(List<GalleryExtensionViewModel> entries)
    {
        switch (SelectedSortOption)
        {
            case ExtensionGallerySortOption.Name:
                entries.Sort(CompareByName);
                break;
            case ExtensionGallerySortOption.Author:
                entries.Sort(CompareByAuthor);
                break;
            case ExtensionGallerySortOption.InstallationStatus:
                entries.Sort(CompareByInstallationStatus);
                break;
            default:
                break;
        }
    }

    private static int Matches(string query, GalleryExtensionViewModel item)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return 100;
        }

        return Contains(item.Title, query)
               || Contains(item.Description, query)
               || Contains(item.AuthorName, query)
               || Contains(item.Tags, query)
            ? 100
            : 0;
    }

    private static bool Contains(string? haystack, string needle)
    {
        return !string.IsNullOrEmpty(haystack) && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Contains(IReadOnlyList<string>? values, string needle)
    {
        if (values is null || values.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < values.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]) && values[i].Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(ItemCounterText));
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ShowNoResultsPanel));
    }

    private void RebuildWinGetEntryIndex()
    {
        _entriesByWinGetId.Clear();

        foreach (var entry in _allEntries)
        {
            if (string.IsNullOrWhiteSpace(entry.WinGetId))
            {
                continue;
            }

            if (!_entriesByWinGetId.TryGetValue(entry.WinGetId, out var entries))
            {
                entries = [];
                _entriesByWinGetId[entry.WinGetId] = entries;
            }

            entries.Add(entry);
        }
    }

    private void ApplyCurrentWinGetOperations()
    {
        if (_winGetOperationTrackerService is null)
        {
            return;
        }

        List<GalleryExtensionViewModel> snapshot;
        lock (_entriesLock)
        {
            snapshot = [.. _allEntries];
        }

        foreach (var entry in snapshot)
        {
            if (string.IsNullOrWhiteSpace(entry.WinGetId))
            {
                continue;
            }

            var operation = _winGetOperationTrackerService.GetLatestOperation(entry.WinGetId);
            if (operation is not null)
            {
                entry.ApplyTrackedOperation(operation);
            }
        }
    }

    private void OnWinGetOperationStarted(object? sender, WinGetPackageOperationEventArgs e)
    {
        QueueTrackedOperationApplication(e.Operation, refreshPackageStatus: false);
    }

    private void OnWinGetOperationUpdated(object? sender, WinGetPackageOperationEventArgs e)
    {
        QueueTrackedOperationApplication(e.Operation, refreshPackageStatus: false);
    }

    private void OnWinGetOperationCompleted(object? sender, WinGetPackageOperationEventArgs e)
    {
        QueueTrackedOperationApplication(e.Operation, refreshPackageStatus: true);
    }

    private void QueueTrackedOperationApplication(WinGetPackageOperation operation, bool refreshPackageStatus)
    {
        _ = Task.Factory.StartNew(
            async () => await ApplyTrackedOperationAsync(operation, refreshPackageStatus),
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach,
            _uiScheduler).Unwrap();
    }

    private async Task ApplyTrackedOperationAsync(WinGetPackageOperation operation, bool refreshPackageStatus)
    {
        List<GalleryExtensionViewModel>? entries;
        lock (_entriesLock)
        {
            if (!_entriesByWinGetId.TryGetValue(operation.PackageId, out entries))
            {
                return;
            }

            // Snapshot to iterate outside the lock
            entries = [.. entries];
        }

        foreach (var entry in entries)
        {
            entry.ApplyTrackedOperation(operation);
        }

        QueueApplyFilter();

        if (!refreshPackageStatus || !operation.IsCompleted || operation.State != WinGetPackageOperationState.Succeeded)
        {
            return;
        }

        foreach (var entry in entries)
        {
            await entry.RefreshWinGetPackageInfoAsync(operation.Kind);
        }
    }

    private void QueueApplyFilter()
    {
        _ = Task.Factory.StartNew(
            ApplyFilter,
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach,
            _uiScheduler);
    }

    private static Task<T> RunInBackgroundAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        return Task.Run(operation, cancellationToken);
    }

    private static int CompareByName(GalleryExtensionViewModel left, GalleryExtensionViewModel right)
    {
        var result = SortStringComparer.Compare(left.DisplayTitle, right.DisplayTitle);
        if (result != 0)
        {
            return result;
        }

        result = SortStringComparer.Compare(left.DisplayAuthorName, right.DisplayAuthorName);
        if (result != 0)
        {
            return result;
        }

        return SortStringComparer.Compare(left.Id, right.Id);
    }

    private static int CompareByAuthor(GalleryExtensionViewModel left, GalleryExtensionViewModel right)
    {
        var result = SortStringComparer.Compare(left.DisplayAuthorName, right.DisplayAuthorName);
        if (result != 0)
        {
            return result;
        }

        return CompareByName(left, right);
    }

    private static int CompareByInstallationStatus(GalleryExtensionViewModel left, GalleryExtensionViewModel right)
    {
        var result = GetInstallationStatusSortRank(left).CompareTo(GetInstallationStatusSortRank(right));
        if (result != 0)
        {
            return result;
        }

        return CompareByName(left, right);
    }

    private static int GetInstallationStatusSortRank(GalleryExtensionViewModel entry)
    {
        if (entry.IsUpdateAvailable)
        {
            return 0;
        }

        if (entry.IsInstalled)
        {
            return 1;
        }

        if (entry.IsInstalledStateKnown)
        {
            return 2;
        }

        return 3;
    }

    partial void OnSelectedSortOptionChanged(ExtensionGallerySortOption value)
    {
        OnPropertyChanged(nameof(IsSortByFeaturedSelected));
        OnPropertyChanged(nameof(IsSortByNameSelected));
        OnPropertyChanged(nameof(IsSortByAuthorSelected));
        OnPropertyChanged(nameof(IsSortByInstallationStatusSelected));
        ApplyFilter();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _cts.Cancel();
        _cts.Dispose();

        if (_winGetOperationTrackerService is not null)
        {
            _winGetOperationTrackerService.OperationStarted -= OnWinGetOperationStarted;
            _winGetOperationTrackerService.OperationUpdated -= OnWinGetOperationUpdated;
            _winGetOperationTrackerService.OperationCompleted -= OnWinGetOperationCompleted;
        }
    }
}
