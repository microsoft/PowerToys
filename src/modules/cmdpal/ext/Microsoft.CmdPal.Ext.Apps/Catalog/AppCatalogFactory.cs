// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.Apps.Catalog.Sources;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

public static class AppCatalogFactory
{
    /// <summary>Creates the default catalog using the supplied settings instance.</summary>
    public static IAppCatalog CreateDefault(AllAppsSettings settings) =>
        CreateDefault(settings, NullLoggerFactory.Instance);

    /// <summary>
    /// Creates the default catalog using the supplied settings and MEL logger factory.
    /// </summary>
    /// <param name="settings">Settings that define catalog sources, filtering, and diagnostics.</param>
    /// <param name="loggerFactory">Factory used to create category-specific diagnostic loggers.</param>
    public static IAppCatalog CreateDefault(
        AllAppsSettings settings,
        MEL.ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        var sourceProvider = new SettingsAppSourceProvider(
            settings,
            new PackagedAppSource(
                new PackageCatalogWrapper(),
                loggerFactory.CreateLogger<PackagedAppSource>()),
            loggerFactory.CreateLogger<Win32AppSource>());

        return new AppCatalog(
            sourceProvider,
            new AppCatalogCache(
                AppCatalogCache.DefaultPath(),
                loggerFactory.CreateLogger<AppCatalogCache>()),
            new SettingsAppVisibilityStore(settings),
            logger: loggerFactory.CreateLogger<AppCatalog>());
    }
}
