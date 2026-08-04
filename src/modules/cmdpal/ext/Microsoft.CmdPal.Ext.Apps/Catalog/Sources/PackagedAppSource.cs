// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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
    private const string SourceId = "packaged";

    private readonly IPackageCatalog _packageCatalog;
    private readonly MEL.ILogger<PackagedAppSource> _logger;
    private bool _disposed;

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

    public event EventHandler<AppSourceInvalidatedEventArgs>? Invalidated;

    public string Id => SourceId;

    public string CacheKey => $"{Environment.OSVersion.Version}|{ThemeHelper.GetCurrentTheme()}";

    public Task<IReadOnlyList<AppCatalogItem>> LoadAsync(CancellationToken cancellationToken)
    {
        return Task.Run<IReadOnlyList<AppCatalogItem>>(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var theme = ThemeHelper.GetCurrentTheme();
                List<AppCatalogItem> items = [];

                foreach (var app in UWP.All())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!app.Enabled)
                    {
                        continue;
                    }

                    try
                    {
                        app.UpdateLogoPath(theme);
                        var snapshot = PackagedAppSnapshot.From(app);
                        var identity = AppIdentity.ForPackaged(app.UserModelId);
                        items.Add(new AppCatalogItem(
                            identity,
                            priority: 0,
                            new AppCatalogSourceReference(SourceId, app.UserModelId),
                            CreateMatchTerms(snapshot),
                            snapshot));
                    }
                    catch (Exception ex)
                    {
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

                return new List<AppCatalogItem>(itemsByIdentity.Values);
            },
            cancellationToken);
    }

    private static List<string> CreateMatchTerms(PackagedAppSnapshot app)
    {
        List<string> terms = [];
        var uniqueTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddMatchTerm(terms, uniqueTerms, app.Name);
        AddMatchTerm(terms, uniqueTerms, app.Description);
        AddMatchTerm(terms, uniqueTerms, app.UserModelId);
        AddMatchTerm(terms, uniqueTerms, app.PackageFamilyName);
        AddMatchTerm(terms, uniqueTerms, app.PackageFullName);
        AddMatchTerm(terms, uniqueTerms, app.PackageLocation);
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
