// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Windows.ApplicationModel;

using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Ext.Apps.Catalog.Sources;

internal sealed partial class PackagedAppSource : IAppSource
{
    public event EventHandler<AppSourceInvalidatedEventArgs>? Invalidated;

    private const string SourceId = "packaged";
    private const int IndexingMaxDegreeOfParallelism = 2;

    private readonly IPackageCatalog _packageCatalog;
    private readonly IPackageManager _packageManager;
    private readonly MEL.ILogger<PackagedAppSource> _logger;
    private Dictionary<string, FileStamp?>? _manifestStamps;
    private AppSourceScanResult? _lastScan;
    private bool _disposed;

    public string Id => SourceId;

    /// <summary>Gets the compatibility key for theme-independent manifest metadata.</summary>
    public string CacheKey => Environment.OSVersion.Version.ToString();

    /// <summary>Initializes a new instance of the <see cref="PackagedAppSource"/> class. Creates current-user package discovery and subscribes to package deployment changes.</summary>
    public PackagedAppSource(
        IPackageCatalog packageCatalog,
        MEL.ILogger<PackagedAppSource>? logger = null,
        IPackageManager? packageManager = null)
    {
        ArgumentNullException.ThrowIfNull(packageCatalog);

        _packageCatalog = packageCatalog;
        _packageManager = packageManager ?? new PackageManagerWrapper();
        _logger = logger ?? NullLogger<PackagedAppSource>.Instance;
        _packageCatalog.PackageInstalling += OnPackageInstalling;
        _packageCatalog.PackageUninstalling += OnPackageUninstalling;
        _packageCatalog.PackageUpdating += OnPackageUpdating;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AppCatalogItem>> LoadAsync(CancellationToken cancellationToken, bool background = false, IReadOnlyList<AppSourcePathChange>? dirtyPaths = null)
    {
        return AppSourceWork.Run<IReadOnlyList<AppCatalogItem>>(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var packages = GetCurrentUserPackages(out var packagesComplete).ToArray();
                var stamps = new Dictionary<string, FileStamp?>(StringComparer.OrdinalIgnoreCase);
                foreach (var package in packages)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var manifestPath = Path.Combine(package.InstalledLocation, "AppxManifest.xml");
                    stamps[$"{package.FullName}|{manifestPath}"] = FileStamp.TryRead(manifestPath);
                }

                if (background && packagesComplete && _lastScan is not null
                    && _manifestStamps is not null && stamps.Count == _manifestStamps.Count
                    && stamps.All(pair => pair.Value is not null
                        && _manifestStamps.TryGetValue(pair.Key, out var previous) && pair.Value == previous))
                {
                    return _lastScan;
                }

                List<AppCatalogItem> items = [];
                var failedFamilies = new ConcurrentBag<string>();
                var retrySource = 0;
                var coverageUnknown = 0;
                var onlyMissingManifests = 1;
                var checkedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var applications = ReadPackages(
                    packages,
                    out var manifestsComplete,
                    background,
                    (package, error) =>
                    {
                        try
                        {
                            failedFamilies.Add(package.FamilyName);
                            var manifestPath = Path.Combine(package.InstalledLocation, "AppxManifest.xml");
                            if (error is not FileNotFoundException and not DirectoryNotFoundException
                                || !stamps.TryGetValue($"{package.FullName}|{manifestPath}", out var stamp)
                                || stamp is not { Exists: false })
                            {
                                Interlocked.Exchange(ref onlyMissingManifests, 0);
                            }
                        }
                        catch (Exception)
                        {
                            Interlocked.Exchange(ref coverageUnknown, 1);
                        }

                        if (PathHelpers.IsRetryableReadFailure(error))
                        {
                            Interlocked.Exchange(ref retrySource, 1);
                        }
                    },
                    cancellationToken);
                var isComplete = packagesComplete && manifestsComplete;
                foreach (var app in applications)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        items.Add(CreateCatalogItem(app));
                        checkedPaths.Add(app.AppUserModelId);
                    }
                    catch (Exception ex)
                    {
                        isComplete = false;
                        onlyMissingManifests = 0;
                        failedFamilies.Add(app.Package.FamilyName);
                        if (PathHelpers.IsRetryableReadFailure(ex))
                        {
                            retrySource = 1;
                        }

                        LogApplicationIndexingFailed(_logger, app.Name, ex);
                    }
                }

                var itemsByIdentity = new Dictionary<string, AppCatalogItem>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in items)
                {
                    if (itemsByIdentity.TryGetValue(item.Identity, out var existing))
                    {
                        itemsByIdentity[item.Identity] = existing.MergeProvenance(item);
                    }
                    else
                    {
                        itemsByIdentity.Add(item.Identity, item);
                    }
                }

                var result = new AppSourceScanResult(
                    new List<AppCatalogItem>(itemsByIdentity.Values),
                    isComplete,
                    retrySource != 0 ? [string.Empty] : [],
                    packagesComplete && coverageUnknown == 0 ? [] : null,
                    checkedPaths,
                    [.. failedFamilies],
                    isFullScan: true);
                if (isComplete || (packagesComplete && coverageUnknown == 0 && onlyMissingManifests != 0 && retrySource == 0))
                {
                    // Retain missing-manifest failures without treating their scan as validated cache data.
                    _manifestStamps = stamps;
                    _lastScan = result;
                }

                return result;
            },
            background,
            cancellationToken);
    }

    /// <summary>Builds an AUMID-identified catalog item with logical logo references and searchable executable aliases.</summary>
    internal static AppCatalogItem CreateCatalogItem(PackagedAppMetadata app)
    {
        var payload = PackagedAppPayload.From(app);
        return new AppCatalogItem(
            AppIdentity.ForPackaged(payload.AppUserModelId),
            priority: 0,
            new AppCatalogSourceReference(SourceId, payload.AppUserModelId),
            CreateMatchTerms(payload, app.ExecutionAliases),
            payload);
    }

    private IEnumerable<IPackage> GetCurrentUserPackages(out bool isComplete)
    {
        isComplete = true;
        List<IPackage> packages = [];
        foreach (var package in _packageManager.FindPackagesForCurrentUser())
        {
            try
            {
                if (!package.IsFramework && !string.IsNullOrEmpty(package.InstalledLocation))
                {
                    packages.Add(package);
                }
            }
            catch (Exception exception)
            {
                isComplete = false;
                ManagedCommon.Logger.LogError(exception.Message);
            }
        }

        return packages;
    }

    private static PackagedAppMetadata[] ReadPackages(
        IEnumerable<IPackage> packages,
        out bool isComplete,
        bool background,
        Action<IPackage, Exception> onError,
        CancellationToken cancellationToken)
    {
        var items = new ConcurrentBag<PackagedAppMetadata>();
        var incomplete = 0;

        if (background)
        {
            foreach (var package in packages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ReadPackage(package);
            }
        }
        else
        {
            Parallel.ForEach(
                packages,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = IndexingMaxDegreeOfParallelism,
                    CancellationToken = cancellationToken,
                },
                ReadPackage);
        }

        isComplete = incomplete == 0;
        return [.. items];

        void ReadPackage(IPackage package)
        {
            try
            {
                var apps = PackagedAppReader.ReadManifest(
                    new PackageMetadata(package),
                    out var complete,
                    exception => onError(package, exception),
                    cancellationToken);
                if (!complete)
                {
                    Interlocked.Exchange(ref incomplete, 1);
                }

                foreach (var app in apps)
                {
                    items.Add(app);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Interlocked.Exchange(ref incomplete, 1);
                onError(package, exception);
                ManagedCommon.Logger.LogError(exception.Message);
            }
        }
    }

    private static List<string> CreateMatchTerms(PackagedAppPayload app, IReadOnlyList<string> executionAliases)
    {
        List<string> terms = [];
        var uniqueTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddMatchTerm(terms, uniqueTerms, app.Name);
        AddMatchTerm(terms, uniqueTerms, app.Description);
        AddMatchTerm(terms, uniqueTerms, app.AppUserModelId);
        AddMatchTerm(terms, uniqueTerms, app.PackageFamilyName);
        AddMatchTerm(terms, uniqueTerms, app.PackageFullName);
        AddMatchTerm(terms, uniqueTerms, app.PackageLocation);
        AddMatchTerm(terms, uniqueTerms, Path.GetFileName(app.Executable));
        foreach (var alias in executionAliases)
        {
            AddMatchTerm(terms, uniqueTerms, alias);
        }

        return terms;
    }

    private static void AddMatchTerm(List<string> terms, HashSet<string> uniqueTerms, string? term)
    {
        if (!string.IsNullOrWhiteSpace(term) && uniqueTerms.Add(term))
        {
            terms.Add(term);
        }
    }

    private void OnPackageInstalling(PackageCatalog sender, PackageInstallingEventArgs args)
    {
        if (args.IsComplete)
        {
            RaiseInvalidated(() => args.Package.IsFramework);
        }
    }

    private void OnPackageUninstalling(PackageCatalog sender, PackageUninstallingEventArgs args)
    {
        if (args.IsComplete)
        {
            RaiseInvalidated(() => args.Package.IsFramework);
        }
    }

    private void OnPackageUpdating(PackageCatalog sender, PackageUpdatingEventArgs args)
    {
        if (args.IsComplete)
        {
            RaiseInvalidated(() => args.TargetPackage.IsFramework);
        }
    }

    private void RaiseInvalidated(Func<bool> getIsFramework)
    {
        if (!_disposed && !PackageWrapper.GetIsFramework(getIsFramework))
        {
            Invalidated?.Invoke(this, AppSourceInvalidatedEventArgs.FullRefresh);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Failed to index packaged application '{AppName}'.")]
    private static partial void LogApplicationIndexingFailed(MEL.ILogger logger, string appName, Exception exception);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _packageCatalog.PackageInstalling -= OnPackageInstalling;
        _packageCatalog.PackageUninstalling -= OnPackageUninstalling;
        _packageCatalog.PackageUpdating -= OnPackageUpdating;
        Invalidated = null;
    }
}
