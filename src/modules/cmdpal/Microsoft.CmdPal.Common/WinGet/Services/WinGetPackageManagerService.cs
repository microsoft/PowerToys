// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Microsoft.CmdPal.Common.WinGet;
using Microsoft.CmdPal.Common.WinGet.Interop;
using Microsoft.CmdPal.Common.WinGet.Models;
using Microsoft.Management.Deployment;
using Windows.Foundation;
using Windows.Foundation.Metadata;

namespace Microsoft.CmdPal.Common.WinGet.Services;

public sealed class WinGetPackageManagerService : IWinGetPackageManagerService
{
    private const string WinGetUnavailableMessage = "WinGet is unavailable. Install or repair App Installer to search and install packages.";
    private const string WinGetCatalogUnavailableMessage = "WinGet couldn't connect to its package catalogs. Check App Installer and your internet connection, then try again.";

    private static readonly StringComparer OrdinalIgnoreCase = StringComparer.OrdinalIgnoreCase;

    private readonly Func<WindowsPackageManagerFactory?> _factoryProvider;
    private readonly WinGetOperationTrackerService _operationTracker;
    private readonly Lazy<InitializationState> _initialization;
    private readonly object _allCatalogTaskLock = new();
    private readonly object _wingetCatalogTaskLock = new();

    private Task<WinGetQueryResult<PackageCatalog>>? _allCatalogTask;
    private Task<WinGetQueryResult<PackageCatalog>>? _wingetCatalogTask;

    public WinGetPackageManagerService()
        : this(CreateFactory, new WinGetOperationTrackerService())
    {
    }

    public WinGetPackageManagerService(WinGetOperationTrackerService operationTracker)
        : this(CreateFactory, operationTracker)
    {
    }

    internal WinGetPackageManagerService(Func<WindowsPackageManagerFactory?>? factoryProvider, WinGetOperationTrackerService? operationTracker = null)
    {
        _factoryProvider = factoryProvider ?? CreateFactory;
        _operationTracker = operationTracker ?? new WinGetOperationTrackerService();
        _initialization = new Lazy<InitializationState>(Initialize, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public WinGetServiceState State => _initialization.Value.State;

    public async Task<WinGetQueryResult<IReadOnlyList<CatalogPackage>>> SearchPackagesAsync(
        string query,
        string? tag = null,
        bool includeStoreCatalog = true,
        uint resultLimit = 25,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) && string.IsNullOrWhiteSpace(tag))
        {
            return new WinGetQueryResult<IReadOnlyList<CatalogPackage>>([], false, null);
        }

        var catalogResult = await GetCompositeCatalogResultAsync(includeStoreCatalog, cancellationToken).ConfigureAwait(false);
        if (!catalogResult.IsSuccess || catalogResult.Value is null)
        {
            return new WinGetQueryResult<IReadOnlyList<CatalogPackage>>(null, catalogResult.IsUnavailable, catalogResult.ErrorMessage);
        }

        var initialization = _initialization.Value;
        if (!initialization.State.IsAvailable || initialization.Factory is null)
        {
            return new WinGetQueryResult<IReadOnlyList<CatalogPackage>>(null, true, initialization.State.Message);
        }

        try
        {
            var options = initialization.Factory.CreateFindPackagesOptions();
            options.ResultLimit = resultLimit;

            if (!string.IsNullOrWhiteSpace(query))
            {
                var selector = initialization.Factory.CreatePackageMatchFilter();
                selector.Field = PackageMatchField.CatalogDefault;
                selector.Value = query;
                selector.Option = PackageFieldMatchOption.ContainsCaseInsensitive;
                options.Selectors.Add(selector);
            }

            if (!string.IsNullOrWhiteSpace(tag))
            {
                var tagFilter = initialization.Factory.CreatePackageMatchFilter();
                tagFilter.Field = PackageMatchField.Tag;
                tagFilter.Value = tag;
                tagFilter.Option = PackageFieldMatchOption.ContainsCaseInsensitive;
                options.Filters.Add(tagFilter);
            }

            var findResult = await Task.Run(() => catalogResult.Value.FindPackages(options), cancellationToken).ConfigureAwait(false);
            if (findResult.Status != FindPackagesResultStatus.Ok)
            {
                return new WinGetQueryResult<IReadOnlyList<CatalogPackage>>(
                    null,
                    false,
                    $"WinGet search failed: {findResult.Status}");
            }

            Dictionary<string, CatalogPackage> results = new(OrdinalIgnoreCase);
            for (var i = 0; i < findResult.Matches.Count; i++)
            {
                var package = findResult.Matches[i].CatalogPackage;
                results.TryAdd(CreatePackageKey(package), package);
            }

            return new WinGetQueryResult<IReadOnlyList<CatalogPackage>>(
                new ReadOnlyCollection<CatalogPackage>(results.Values.ToList()),
                false,
                null);
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or TaskCanceledException)
        {
            CoreLogger.LogWarning($"WinGet search failed: {ex.Message}");
            return new WinGetQueryResult<IReadOnlyList<CatalogPackage>>(null, false, ex.Message);
        }
    }

    public async Task<WinGetQueryResult<IReadOnlyList<WinGetExtensionCatalogEntry>>> SearchCommandPaletteExtensionsAsync(
        uint resultLimit = 100,
        CancellationToken cancellationToken = default)
    {
        var searchResult = await SearchPackagesAsync(
            query: string.Empty,
            tag: WinGetPackageTags.CommandPaletteExtension,
            includeStoreCatalog: false,
            resultLimit: resultLimit,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!searchResult.IsSuccess)
        {
            return new WinGetQueryResult<IReadOnlyList<WinGetExtensionCatalogEntry>>(
                null,
                searchResult.IsUnavailable,
                searchResult.ErrorMessage);
        }

        if (searchResult.Value is null || searchResult.Value.Count == 0)
        {
            return new WinGetQueryResult<IReadOnlyList<WinGetExtensionCatalogEntry>>([], false, null);
        }

        List<WinGetExtensionCatalogEntry> entries = new(searchResult.Value.Count);
        for (var i = 0; i < searchResult.Value.Count; i++)
        {
            entries.Add(WinGetPackageMetadataHelper.CreateExtensionCatalogEntry(searchResult.Value[i]));
        }

        return new WinGetQueryResult<IReadOnlyList<WinGetExtensionCatalogEntry>>(
            new ReadOnlyCollection<WinGetExtensionCatalogEntry>(entries),
            false,
            null);
    }

    public async Task<WinGetQueryResult<IReadOnlyDictionary<string, CatalogPackage>>> GetPackagesByIdAsync(
        IEnumerable<string> packageIds,
        bool includeStoreCatalog = false,
        CancellationToken cancellationToken = default)
    {
        var normalizedIds = NormalizePackageIds(packageIds);
        if (normalizedIds.Count == 0)
        {
            return new WinGetQueryResult<IReadOnlyDictionary<string, CatalogPackage>>(
                new Dictionary<string, CatalogPackage>(OrdinalIgnoreCase),
                false,
                null);
        }

        var catalogResult = await GetCompositeCatalogResultAsync(includeStoreCatalog, cancellationToken).ConfigureAwait(false);
        if (!catalogResult.IsSuccess || catalogResult.Value is null)
        {
            return new WinGetQueryResult<IReadOnlyDictionary<string, CatalogPackage>>(null, catalogResult.IsUnavailable, catalogResult.ErrorMessage);
        }

        var initialization = _initialization.Value;
        if (!initialization.State.IsAvailable || initialization.Factory is null)
        {
            return new WinGetQueryResult<IReadOnlyDictionary<string, CatalogPackage>>(null, true, initialization.State.Message);
        }

        try
        {
            var options = initialization.Factory.CreateFindPackagesOptions();
            options.ResultLimit = (uint)normalizedIds.Count;

            for (var i = 0; i < normalizedIds.Count; i++)
            {
                var selector = initialization.Factory.CreatePackageMatchFilter();
                selector.Field = PackageMatchField.Id;
                selector.Option = PackageFieldMatchOption.EqualsCaseInsensitive;
                selector.Value = normalizedIds[i];
                options.Selectors.Add(selector);
            }

            var findResult = await Task.Run(() => catalogResult.Value.FindPackages(options), cancellationToken).ConfigureAwait(false);
            if (findResult.Status != FindPackagesResultStatus.Ok)
            {
                return new WinGetQueryResult<IReadOnlyDictionary<string, CatalogPackage>>(
                    null,
                    false,
                    $"WinGet package lookup failed: {findResult.Status}");
            }

            Dictionary<string, CatalogPackage> results = new(OrdinalIgnoreCase);
            for (var i = 0; i < findResult.Matches.Count; i++)
            {
                var package = findResult.Matches[i].CatalogPackage;
                if (!results.ContainsKey(package.Id))
                {
                    results[package.Id] = package;
                }
            }

            return new WinGetQueryResult<IReadOnlyDictionary<string, CatalogPackage>>(results, false, null);
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or TaskCanceledException)
        {
            CoreLogger.LogWarning($"WinGet package lookup failed: {ex.Message}");
            return new WinGetQueryResult<IReadOnlyDictionary<string, CatalogPackage>>(null, false, ex.Message);
        }
    }

    public async Task<WinGetPackageOperationResult> InstallPackageAsync(
        CatalogPackage package,
        bool skipDependencies = false,
        Action<InstallProgress>? progressHandler = null,
        CancellationToken cancellationToken = default)
    {
        var trackedOperation = _operationTracker.StartOperation(
            package.Id,
            WinGetPackageMetadataHelper.GetPackageDisplayName(package),
            WinGetPackageOperationKind.Install);

        var initialization = _initialization.Value;
        if (!initialization.State.IsAvailable || initialization.Factory is null || initialization.PackageManager is null)
        {
            _operationTracker.CompleteOperation(trackedOperation.OperationId, WinGetPackageOperationState.Failed, initialization.State.Message);
            return new WinGetPackageOperationResult(false, true, initialization.State.Message);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var installOptions = initialization.Factory.CreateInstallOptions();
            installOptions.PackageInstallScope = PackageInstallScope.Any;
            installOptions.SkipDependencies = skipDependencies;

            var operation = initialization.PackageManager.InstallPackageAsync(package, installOptions);
            _operationTracker.RegisterCancellationHandler(trackedOperation.OperationId, operation.Cancel);
            operation.Progress = new AsyncOperationProgressHandler<InstallResult, InstallProgress>((_, progress) =>
            {
                UpdateTrackedInstallOperation(trackedOperation.OperationId, progress);
                progressHandler?.Invoke(progress);
            });

            await operation.AsTask().ConfigureAwait(false);
            await RefreshCatalogsAsync(cancellationToken).ConfigureAwait(false);
            _operationTracker.CompleteOperation(trackedOperation.OperationId, WinGetPackageOperationState.Succeeded);

            return new WinGetPackageOperationResult(true, false, null);
        }
        catch (OperationCanceledException ex)
        {
            CoreLogger.LogWarning($"WinGet install canceled for '{package.Id}': {ex.Message}");
            _operationTracker.CompleteOperation(trackedOperation.OperationId, WinGetPackageOperationState.Canceled, ex.Message);
            return new WinGetPackageOperationResult(false, false, ex.Message);
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or TaskCanceledException)
        {
            CoreLogger.LogWarning($"WinGet install failed for '{package.Id}': {ex.Message}");
            _operationTracker.CompleteOperation(trackedOperation.OperationId, WinGetPackageOperationState.Failed, ex.Message);
            return new WinGetPackageOperationResult(false, false, ex.Message);
        }
    }

    public async Task<WinGetPackageOperationResult> UninstallPackageAsync(
        CatalogPackage package,
        Action<UninstallProgress>? progressHandler = null,
        CancellationToken cancellationToken = default)
    {
        var trackedOperation = _operationTracker.StartOperation(
            package.Id,
            WinGetPackageMetadataHelper.GetPackageDisplayName(package),
            WinGetPackageOperationKind.Uninstall);

        var initialization = _initialization.Value;
        if (!initialization.State.IsAvailable || initialization.Factory is null || initialization.PackageManager is null)
        {
            _operationTracker.CompleteOperation(trackedOperation.OperationId, WinGetPackageOperationState.Failed, initialization.State.Message);
            return new WinGetPackageOperationResult(false, true, initialization.State.Message);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var uninstallOptions = initialization.Factory.CreateUninstallOptions();
            uninstallOptions.PackageUninstallScope = PackageUninstallScope.Any;

            var operation = initialization.PackageManager.UninstallPackageAsync(package, uninstallOptions);
            _operationTracker.RegisterCancellationHandler(trackedOperation.OperationId, operation.Cancel);
            operation.Progress = new AsyncOperationProgressHandler<UninstallResult, UninstallProgress>((_, progress) =>
            {
                UpdateTrackedUninstallOperation(trackedOperation.OperationId, progress);
                progressHandler?.Invoke(progress);
            });

            await operation.AsTask().ConfigureAwait(false);
            await RefreshCatalogsAsync(cancellationToken).ConfigureAwait(false);
            _operationTracker.CompleteOperation(trackedOperation.OperationId, WinGetPackageOperationState.Succeeded);

            return new WinGetPackageOperationResult(true, false, null);
        }
        catch (OperationCanceledException ex)
        {
            CoreLogger.LogWarning($"WinGet uninstall canceled for '{package.Id}': {ex.Message}");
            _operationTracker.CompleteOperation(trackedOperation.OperationId, WinGetPackageOperationState.Canceled, ex.Message);
            return new WinGetPackageOperationResult(false, false, ex.Message);
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or TaskCanceledException)
        {
            CoreLogger.LogWarning($"WinGet uninstall failed for '{package.Id}': {ex.Message}");
            _operationTracker.CompleteOperation(trackedOperation.OperationId, WinGetPackageOperationState.Failed, ex.Message);
            return new WinGetPackageOperationResult(false, false, ex.Message);
        }
    }

    public async Task<bool> RefreshCatalogsAsync(CancellationToken cancellationToken = default)
    {
        ClearCompositeCatalogCache();

        var initialization = _initialization.Value;
        if (!initialization.State.IsAvailable || initialization.AvailableCatalogs.Count == 0)
        {
            return false;
        }

        if (!ApiInformation.IsApiContractPresent("Microsoft.Management.Deployment.WindowsPackageManagerContract", 12))
        {
            return false;
        }

        try
        {
            for (var i = 0; i < initialization.AvailableCatalogs.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await initialization.AvailableCatalogs[i].RefreshPackageCatalogAsync().AsTask().ConfigureAwait(false);
            }

            return true;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or TaskCanceledException)
        {
            CoreLogger.LogWarning($"WinGet catalog refresh failed: {ex.Message}");
            return false;
        }
    }

    private static WindowsPackageManagerFactory? CreateFactory()
    {
        try
        {
            return new WindowsPackageManagerStandardFactory();
        }
        catch (Exception ex)
        {
            CoreLogger.LogWarning($"Failed to initialize WinGet API factory: {ex.Message}");
            return null;
        }
    }

    private InitializationState Initialize()
    {
        try
        {
            var factory = _factoryProvider();
            if (factory is null)
            {
                return InitializationState.Unavailable(WinGetUnavailableMessage);
            }

            var packageManager = factory.CreatePackageManager();
            var wingetCatalog = packageManager.GetPredefinedPackageCatalog(PredefinedPackageCatalog.OpenWindowsCatalog);

            List<PackageCatalogReference> availableCatalogs = [wingetCatalog];
            PackageCatalogReference? storeCatalog = null;

            try
            {
                storeCatalog = packageManager.GetPredefinedPackageCatalog(PredefinedPackageCatalog.MicrosoftStore);
                availableCatalogs.Add(storeCatalog);
            }
            catch (Exception ex)
            {
                CoreLogger.LogWarning($"Failed to initialize Microsoft Store catalog: {ex.Message}");
            }

            if (ApiInformation.IsApiContractPresent("Microsoft.Management.Deployment.WindowsPackageManagerContract", 8))
            {
                foreach (var catalogReference in availableCatalogs)
                {
                    catalogReference.PackageCatalogBackgroundUpdateInterval = new(0);
                }
            }

            return InitializationState.Available(
                factory,
                packageManager,
                wingetCatalog,
                storeCatalog,
                availableCatalogs);
        }
        catch (Exception ex)
        {
            CoreLogger.LogWarning($"Failed to initialize WinGet package manager: {ex.Message}");
            return InitializationState.Unavailable(WinGetUnavailableMessage);
        }
    }

    private async Task<WinGetQueryResult<PackageCatalog>> GetCompositeCatalogResultAsync(bool includeStoreCatalog, CancellationToken cancellationToken)
    {
        Task<WinGetQueryResult<PackageCatalog>> task;
        if (includeStoreCatalog)
        {
            lock (_allCatalogTaskLock)
            {
                _allCatalogTask ??= CreateCompositeCatalogAsync(includeStoreCatalog, cancellationToken);
                task = _allCatalogTask;
            }
        }
        else
        {
            lock (_wingetCatalogTaskLock)
            {
                _wingetCatalogTask ??= CreateCompositeCatalogAsync(includeStoreCatalog, cancellationToken);
                task = _wingetCatalogTask;
            }
        }

        var result = await task.ConfigureAwait(false);
        if (!result.IsSuccess || result.Value is null)
        {
            ClearCachedCompositeCatalogTask(includeStoreCatalog, task);
        }

        return result;
    }

    private async Task<WinGetQueryResult<PackageCatalog>> CreateCompositeCatalogAsync(bool includeStoreCatalog, CancellationToken cancellationToken)
    {
        var initialization = _initialization.Value;
        if (!initialization.State.IsAvailable || initialization.Factory is null || initialization.PackageManager is null || initialization.WingetCatalog is null)
        {
            return new WinGetQueryResult<PackageCatalog>(null, true, initialization.State.Message);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var options = initialization.Factory.CreateCreateCompositePackageCatalogOptions();
            options.CompositeSearchBehavior = CompositeSearchBehavior.RemotePackagesFromAllCatalogs;
            options.Catalogs.Add(initialization.WingetCatalog);

            if (includeStoreCatalog && initialization.StoreCatalog is not null)
            {
                options.Catalogs.Add(initialization.StoreCatalog);
            }

            var compositeCatalogReference = initialization.PackageManager.CreateCompositePackageCatalog(options);
            var connectResult = await compositeCatalogReference.ConnectAsync().AsTask().ConfigureAwait(false);
            if (connectResult.Status != ConnectResultStatus.Ok || connectResult.PackageCatalog is null)
            {
                var message = connectResult.Status == ConnectResultStatus.CatalogError ?
                    WinGetCatalogUnavailableMessage :
                    $"WinGet catalog connection failed: {connectResult.Status}";
                CoreLogger.LogWarning(message);
                return new WinGetQueryResult<PackageCatalog>(null, false, message);
            }

            return new WinGetQueryResult<PackageCatalog>(connectResult.PackageCatalog, false, null);
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or TaskCanceledException)
        {
            CoreLogger.LogWarning($"Failed to create WinGet composite catalog: {ex.Message}");
            return new WinGetQueryResult<PackageCatalog>(null, false, ex.Message);
        }
    }

    private void ClearCompositeCatalogCache()
    {
        lock (_allCatalogTaskLock)
        {
            _allCatalogTask = null;
        }

        lock (_wingetCatalogTaskLock)
        {
            _wingetCatalogTask = null;
        }
    }

    private void ClearCachedCompositeCatalogTask(bool includeStoreCatalog, Task<WinGetQueryResult<PackageCatalog>> task)
    {
        if (includeStoreCatalog)
        {
            lock (_allCatalogTaskLock)
            {
                if (ReferenceEquals(_allCatalogTask, task))
                {
                    _allCatalogTask = null;
                }
            }
        }
        else
        {
            lock (_wingetCatalogTaskLock)
            {
                if (ReferenceEquals(_wingetCatalogTask, task))
                {
                    _wingetCatalogTask = null;
                }
            }
        }
    }

    private static List<string> NormalizePackageIds(IEnumerable<string> packageIds)
    {
        List<string> normalized = [];
        HashSet<string> seen = new(OrdinalIgnoreCase);

        foreach (var candidate in packageIds)
        {
            var trimmed = ToNullIfWhiteSpace(candidate);
            if (trimmed is null || !seen.Add(trimmed))
            {
                continue;
            }

            normalized.Add(trimmed);
        }

        return normalized;
    }

    private static string CreatePackageKey(CatalogPackage package)
    {
        var catalogId =
            TryGetCatalogId(() => package.DefaultInstallVersion?.PackageCatalog?.Info?.Id)
            ?? TryGetCatalogId(() => package.InstalledVersion?.PackageCatalog?.Info?.Id)
            ?? string.Empty;

        return string.Concat(package.Id, "\u001F", catalogId);
    }

    private static string? TryGetCatalogId(Func<string?> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }

    private static string? ToNullIfWhiteSpace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private void UpdateTrackedInstallOperation(Guid operationId, InstallProgress progress)
    {
        switch (progress.State)
        {
            case PackageInstallProgressState.Queued:
                _operationTracker.UpdateOperation(operationId, WinGetPackageOperationState.Queued, isIndeterminate: true);
                break;
            case PackageInstallProgressState.Downloading:
            {
                var progressPercent = progress.BytesRequired > 0
                    ? (uint?)Math.Min(100, (progress.BytesDownloaded * 100UL) / progress.BytesRequired)
                    : null;
                _operationTracker.UpdateOperation(
                    operationId,
                    WinGetPackageOperationState.Downloading,
                    isIndeterminate: progress.BytesRequired == 0,
                    progressPercent: progressPercent,
                    bytesDownloaded: progress.BytesDownloaded,
                    bytesRequired: progress.BytesRequired);
                break;
            }

            case PackageInstallProgressState.Installing:
                _operationTracker.UpdateOperation(operationId, WinGetPackageOperationState.Installing, isIndeterminate: true);
                break;
            case PackageInstallProgressState.PostInstall:
            case PackageInstallProgressState.Finished:
                _operationTracker.UpdateOperation(operationId, WinGetPackageOperationState.PostProcessing, isIndeterminate: true);
                break;
            default:
                break;
        }
    }

    private void UpdateTrackedUninstallOperation(Guid operationId, UninstallProgress progress)
    {
        switch (progress.State)
        {
            case PackageUninstallProgressState.Queued:
                _operationTracker.UpdateOperation(operationId, WinGetPackageOperationState.Queued, isIndeterminate: true);
                break;
            case PackageUninstallProgressState.Uninstalling:
                _operationTracker.UpdateOperation(operationId, WinGetPackageOperationState.Uninstalling, isIndeterminate: true);
                break;
            case PackageUninstallProgressState.PostUninstall:
            case PackageUninstallProgressState.Finished:
                _operationTracker.UpdateOperation(operationId, WinGetPackageOperationState.PostProcessing, isIndeterminate: true);
                break;
            default:
                break;
        }
    }

    private sealed record InitializationState(
        WinGetServiceState State,
        WindowsPackageManagerFactory? Factory,
        PackageManager? PackageManager,
        PackageCatalogReference? WingetCatalog,
        PackageCatalogReference? StoreCatalog,
        IReadOnlyList<PackageCatalogReference> AvailableCatalogs)
    {
        public static InitializationState Available(
            WindowsPackageManagerFactory factory,
            PackageManager packageManager,
            PackageCatalogReference wingetCatalog,
            PackageCatalogReference? storeCatalog,
            IReadOnlyList<PackageCatalogReference> availableCatalogs)
        {
            return new InitializationState(
                new WinGetServiceState(true, Message: null),
                factory,
                packageManager,
                wingetCatalog,
                storeCatalog,
                availableCatalogs);
        }

        public static InitializationState Unavailable(string message)
        {
            return new InitializationState(
                new WinGetServiceState(false, message),
                Factory: null,
                PackageManager: null,
                WingetCatalog: null,
                StoreCatalog: null,
                AvailableCatalogs: []);
        }
    }
}
