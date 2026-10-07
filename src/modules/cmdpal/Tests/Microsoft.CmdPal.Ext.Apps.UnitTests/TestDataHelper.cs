// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Programs;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

/// <summary>
/// Helper class to create test data for unit tests.
/// </summary>
public static class TestDataHelper
{
    internal static FuzzyMatcherProvider CreateFuzzyMatcherProvider()
    {
        return new(new(), new());
    }

    internal static FrozenDictionary<string, string> RetainCommandAliases(AppCommandAliasStore store, IEnumerable<AppItem> apps)
    {
        var aliases = AppCatalogCommandAliases.Retain(store.GetSnapshot(), apps);
        store.SetSnapshot(aliases);
        return aliases;
    }

    internal static AppVisibility GetVisibility(
        IAppVisibilityStore store,
        AppCatalogItem item,
        AllAppsSettings settings = null,
        AppCommandAliasStore aliases = null)
    {
        var rules = new AppCatalogVisibility(settings?.ExcludedAppNames ?? [], settings?.ExcludedAppPaths ?? []);
        var hiddenIdentities = store.GetSnapshot();
        var hiddenCommands = AppCatalogVisibility.ResolveHiddenCommandIds(
            hiddenIdentities, aliases?.GetSnapshot() ?? FrozenDictionary<string, string>.Empty);
        return rules.GetVisibility(item, hiddenIdentities, hiddenCommands);
    }

    internal static bool SetHidden(
        IAppVisibilityStore store,
        AppCatalogItem item,
        bool hidden,
        AppCommandAliasStore aliases = null)
    {
        return store.SetSnapshot(AppCatalogVisibility.UpdateHiddenIdentities(
            store.GetSnapshot(), item, hidden, aliases?.GetSnapshot() ?? FrozenDictionary<string, string>.Empty));
    }

    internal static string GetAliasesPath(string settingsPath)
    {
        return Path.ChangeExtension(settingsPath, "aliases.json");
    }

    internal static string GetVisibilityPath(string settingsPath)
    {
        return Path.ChangeExtension(settingsPath, "visibility.json");
    }

    internal static void WriteHiddenIdentities(string settingsPath, params string[] identities)
    {
        var values = new JsonArray();
        foreach (var identity in identities)
        {
            values.Add((JsonNode)JsonValue.Create(identity)!);
        }

        File.WriteAllText(GetVisibilityPath(settingsPath), new JsonObject { ["HiddenAppIdentities"] = values }.ToJsonString());
    }

    internal static void DeleteSettingsFiles(string settingsPath)
    {
        File.Delete(settingsPath);
        File.Delete(GetAliasesPath(settingsPath));
        File.Delete(GetVisibilityPath(settingsPath));
    }

    /// <summary>
    /// Creates transient Win32 metadata for catalog indexing tests.
    /// </summary>
    /// <param name="name">The name of the application.</param>
    /// <param name="fullPath">The full path to the application executable.</param>
    /// <param name="valid">A value indicating whether the application is valid.</param>
    /// <returns>A new Win32AppMetadata instance with the specified parameters.</returns>
    internal static Win32AppMetadata CreateTestWin32Metadata(
        string name = "Test App",
        string fullPath = "C:\\TestApp\\app.exe",
        bool valid = true)
    {
        return new Win32AppMetadata
        {
            Name = name,
            TargetPath = fullPath,
            Valid = valid,
            Description = $"Test description for {name}",
            SourceFilename = "app.exe",
            ParentDirectory = "C:\\TestApp",
            AppType = Win32AppType.Win32Application,
        };
    }

    /// <summary>
    /// Creates transient packaged metadata for catalog indexing tests.
    /// </summary>
    /// <param name="displayName">The display name of the application.</param>
    /// <param name="userModelId">The user model ID of the application.</param>
    /// <param name="packageLocation">The package installation directory.</param>
    /// <returns>A new PackagedAppMetadata instance with the specified parameters.</returns>
    internal static PackagedAppMetadata CreateTestPackagedMetadata(
        string displayName = "Test UWP App",
        string userModelId = "TestPublisher.TestUWPApp_1.0.0.0_neutral__8wekyb3d8bbwe",
        string packageLocation = null)
    {
        return new PackagedAppMetadata
        {
            Name = displayName,
            AppUserModelId = userModelId,
            Description = $"Test UWP description for {displayName}",
            CanRunElevated = false,
            Package = CreateMockPackageMetadata(displayName, userModelId, packageLocation),
        };
    }

    /// <summary>
    /// Creates package metadata for testing purposes.
    /// </summary>
    /// <param name="displayName">The display name of the package.</param>
    /// <param name="userModelId">The user model ID of the package.</param>
    /// <param name="packageLocation">An optional package installation directory.</param>
    /// <returns>New package metadata.</returns>
    private static PackageMetadata CreateMockPackageMetadata(string displayName, string userModelId, string packageLocation)
    {
        var mockPackage = new MockPackage
        {
            Name = displayName,
            FullName = userModelId,
            FamilyName = $"{displayName}_8wekyb3d8bbwe",
            InstalledLocation = packageLocation ?? $"C:\\Program Files\\WindowsApps\\{displayName}",
        };

        return new PackageMetadata(mockPackage);
    }

    /// <summary>
    /// Mock implementation of IPackage for testing purposes.
    /// </summary>
    private sealed class MockPackage : IPackage
    {
        /// <summary>
        /// Gets or sets the name of the package.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the full name of the package.
        /// </summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the family name of the package.
        /// </summary>
        public string FamilyName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the package is a framework package.
        /// </summary>
        public bool IsFramework { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the package is in development mode.
        /// </summary>
        public bool IsDevelopmentMode { get; set; }

        /// <summary>
        /// Gets or sets the installed location of the package.
        /// </summary>
        public string InstalledLocation { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the package is a non-removable system component.
        /// </summary>
        public bool IsNonRemovable { get; set; }
    }
}
