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

    private readonly IPackageCatalog _packageCatalog;
    private readonly MEL.ILogger<PackagedAppSource> _logger;
    private Dictionary<string, FileStamp?>? _manifestStamps;
    private IReadOnlyList<AppCatalogItem>? _lastCompleteItems;
    private string? _lastCacheKey;
    private bool _disposed;

    public string Id => SourceId;

    public string CacheKey => $"{Environment.OSVersion.Version}|{ThemeHelper.GetCurrentTheme()}";

    public PackagedAppSource(
        IPackageCatalog packageCatalog,
        MEL.ILogger<PackagedAppSource>? logger = null)
    {
        _packageCatalog = packageCatalog ?? throw new ArgumentNullException(nameof(packageCatalog));
        _logger = logger ?? NullLogger<PackagedAppSource>.Instance;
        _packageCatalog.PackageInstalling += OnPackageInstalling;
        _packageCatalog.PackageUninstalling += OnPackageUninstalling;
        _packageCatalog.PackageUpdating += OnPackageUpdating;
    }

    public Task<IReadOnlyList<AppCatalogItem>> LoadAsync(CancellationToken cancellationToken, bool background = false)
    {
        return AppSourceWork.Run<IReadOnlyList<AppCatalogItem>>(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var theme = ThemeHelper.GetCurrentTheme();
                var cacheKey = $"{Environment.OSVersion.Version}|{theme}";
                var packages = UWP.CurrentUserPackages(out var packagesComplete).ToArray();
                var stamps = new Dictionary<string, FileStamp?>(StringComparer.OrdinalIgnoreCase);
                foreach (var package in packages)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var manifestPath = Path.Combine(package.InstalledLocation, "AppxManifest.xml");
                    stamps[$"{package.FullName}|{manifestPath}"] = FileStamp.TryRead(manifestPath);
                }

                if (background && packagesComplete && _lastCompleteItems is not null
                    && string.Equals(cacheKey, _lastCacheKey, StringComparison.Ordinal)
                    && _manifestStamps is not null && stamps.Count == _manifestStamps.Count
                    && stamps.All(pair => pair.Value is { Exists: true }
                        && _manifestStamps.TryGetValue(pair.Key, out var previous) && pair.Value == previous))
                {
                    return _lastCompleteItems;
                }

                List<AppCatalogItem> items = [];
                var failedFamilies = new ConcurrentBag<string>();
                var retrySource = 0;
                var coverageUnknown = 0;
                var checkedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var applications = UWP.All(
                    packages,
                    out var manifestsComplete,
                    background,
                    (package, error) =>
                    {
                        try
                        {
                            failedFamilies.Add(package.FamilyName);
                        }
                        catch (Exception)
                        {
                            Interlocked.Exchange(ref coverageUnknown, 1);
                        }

                        if (Win32Program.IsRetryableReadFailure(error))
                        {
                            Interlocked.Exchange(ref retrySource, 1);
                        }
                    },
                    cancellationToken);
                var isComplete = packagesComplete && manifestsComplete;
                foreach (var app in applications)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!app.Enabled)
                    {
                        checkedPaths.Add(app.UserModelId);
                        continue;
                    }

                    try
                    {
                        app.UpdateLogoPath(theme);
                        items.Add(CreateCatalogItem(app));
                        checkedPaths.Add(app.UserModelId);
                    }
                    catch (Exception ex)
                    {
                        isComplete = false;
                        failedFamilies.Add(app.Package.FamilyName);
                        if (Win32Program.IsRetryableReadFailure(ex))
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
                if (isComplete)
                {
                    _manifestStamps = stamps;
                    _lastCompleteItems = result;
                    _lastCacheKey = cacheKey;
                }

                return result;
            },
            background,
            cancellationToken);
    }

    internal static AppCatalogItem CreateCatalogItem(IUWPApplication app)
    {
        var snapshot = PackagedAppSnapshot.From(app);
        return new AppCatalogItem(
            AppIdentity.ForPackaged(snapshot.UserModelId),
            priority: 0,
            new AppCatalogSourceReference(SourceId, snapshot.UserModelId),
            CreateMatchTerms(snapshot, app.ExecutionAliases),
            snapshot);
    }

    private static List<string> CreateMatchTerms(PackagedAppSnapshot app, IReadOnlyList<string> executionAliases)
    {
        List<string> terms = [];
        var uniqueTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddMatchTerm(terms, uniqueTerms, app.Name);
        AddMatchTerm(terms, uniqueTerms, app.Description);
        AddMatchTerm(terms, uniqueTerms, app.UserModelId);
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
