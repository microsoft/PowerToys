// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Windows.Win32;
using Windows.Win32.Storage.Packaging.Appx;
using Windows.Win32.System.Com;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

[Serializable]
public partial class UWP
{
    private const int IndexingMaxDegreeOfParallelism = 2;

    private static readonly IPath Path = new FileSystem().Path;

    private static readonly XNamespace Uap3Namespace = "http://schemas.microsoft.com/appx/manifest/uap/windows10/3";
    private static readonly XNamespace Uap5Namespace = "http://schemas.microsoft.com/appx/manifest/uap/windows10/5";
    private static readonly XNamespace Uap8Namespace = "http://schemas.microsoft.com/appx/manifest/uap/windows10/8";
    private static readonly XNamespace DesktopNamespace = "http://schemas.microsoft.com/appx/manifest/desktop/windows10";

    private static readonly Dictionary<string, PackageVersion> _versionFromNamespace = new()
    {
        { "http://schemas.microsoft.com/appx/manifest/foundation/windows10", PackageVersion.Windows10 },
        { "http://schemas.microsoft.com/appx/2013/manifest", PackageVersion.Windows81 },
        { "http://schemas.microsoft.com/appx/2010/manifest", PackageVersion.Windows8 },
    };

    public string Name { get; }

    public string FullName { get; }

    public string FamilyName { get; }

    public string Location { get; set; } = string.Empty;

    // Localized path based on windows display language
    public string LocationLocalized { get; set; } = string.Empty;

    public IList<UWPApplication> Apps { get; private set; } = new List<UWPApplication>();

    public PackageVersion Version { get; set; }

    public static IPackageManager PackageManagerWrapper { get; set; } = new PackageManagerWrapper();

    public bool IsNonRemovable { get; }

    public UWP(IPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        Name = package.Name;
        FullName = package.FullName;
        FamilyName = package.FamilyName;
        IsNonRemovable = package.IsNonRemovable;
    }

    public unsafe void InitializeAppInfo(string installedLocation)
    {
        _ = TryInitializeAppInfo(installedLocation);
    }

    private unsafe bool TryInitializeAppInfo(string installedLocation, Action<Exception>? onError = null)
    {
        Location = installedLocation;
        LocationLocalized = ShellLocalization.Instance.GetLocalizedPath(installedLocation);
        var path = Path.Combine(installedLocation, "AppxManifest.xml");

        XDocument manifest;
        using (var manifestStream = System.IO.File.Open(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete))
        {
            manifest = XDocument.Load(manifestStream);
        }

        var namespaces = XmlNamespaces(manifest);
        InitPackageVersion(namespaces);
        var executionAliases = GetExecutionAliases(manifest);

        const uint noAttribute = 0x80;
        const uint readSharing = 0x00000040; // STGM_READ | STGM_SHARE_DENY_NONE
        try
        {
            IStream* stream = null;
            PInvoke.SHCreateStreamOnFileEx(path, readSharing, noAttribute, false, null, &stream).ThrowOnFailure();
            using var streamHandle = new SafeComHandle((IntPtr)stream);

            var appsInManifest = AppxPackageHelper.GetAppsFromManifest(stream);

            foreach (var appInManifest in appsInManifest)
            {
                using var appHandle = new SafeComHandle(appInManifest);
                var manifestApp = (IAppxManifestApplication*)appInManifest;
                var uwpApp = new UWPApplication(manifestApp, this);
                var idResult = manifestApp->GetStringValue("ID", out var idPtr);
                try
                {
                    if (idResult.Succeeded && executionAliases.TryGetValue(idPtr.ToString(), out var aliases))
                    {
                        uwpApp.ExecutionAliases = aliases;
                    }
                }
                finally
                {
                    PInvoke.CoTaskMemFree(idPtr);
                }

                if (!string.IsNullOrEmpty(uwpApp.UserModelId) &&
                    !string.IsNullOrEmpty(uwpApp.DisplayName) &&
                    uwpApp.AppListEntry != "none")
                {
                    Apps.Add(uwpApp);
                }
            }
        }
        catch (Exception ex)
        {
            Apps = [];
            onError?.Invoke(ex);
            Logger.LogError($"Failed to initialize UWP app info for {Name} ({FullName}): {ex.Message}");
            return false;
        }

        return true;
    }

    private static string[] XmlNamespaces(XDocument manifest)
    {
        if (manifest.Root is not null)
        {
            var namespaces = new HashSet<string>();

            var attributes = manifest.Root.Attributes();
            foreach (var attribute in attributes)
            {
                if (attribute.IsNamespaceDeclaration)
                {
                    // Extract namespace
                    var key = attribute.Name.Namespace == XNamespace.None ? string.Empty : attribute.Name.LocalName;
                    XNamespace ns = XNamespace.Get(attribute.Value);
                    var nsString = ns.ToString();

                    // Use HashSet to check for duplicates
                    namespaces.Add(nsString);
                }
            }

            var uniqueNamespaces = new string[namespaces.Count];
            namespaces.CopyTo(uniqueNamespaces);
            return uniqueNamespaces;
        }
        else
        {
            return [];
        }
    }

    private static Dictionary<string, IReadOnlyList<string>> GetExecutionAliases(XDocument manifest)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var root = manifest.Root;
        if (root is null)
        {
            return result;
        }

        var ns = root.Name.Namespace;
        foreach (var application in root.Element(ns + "Applications")?.Elements(ns + "Application") ?? [])
        {
            var id = (string?)application.Attribute("Id");
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var extension in application.Element(ns + "Extensions")?.Elements() ?? [])
            {
                if ((extension.Name != Uap3Namespace + "Extension" && extension.Name != Uap5Namespace + "Extension")
                    || (string?)extension.Attribute("Category") != "windows.appExecutionAlias")
                {
                    continue;
                }

                foreach (var aliasElement in extension.Element(extension.Name.Namespace + "AppExecutionAlias")?.Elements() ?? [])
                {
                    if (aliasElement.Name != DesktopNamespace + "ExecutionAlias"
                        && aliasElement.Name != Uap5Namespace + "ExecutionAlias"
                        && aliasElement.Name != Uap8Namespace + "ExecutionAlias")
                    {
                        continue;
                    }

                    var alias = (string?)aliasElement.Attribute("Alias");
                    if (!string.IsNullOrWhiteSpace(alias)
                        && alias.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                        && alias.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
                    {
                        aliases.Add(alias);
                    }
                }
            }

            if (aliases.Count > 0)
            {
                var names = new string[aliases.Count];
                aliases.CopyTo(names);
                result[id] = names;
            }
        }

        return result;
    }

    private void InitPackageVersion(string[] namespaces)
    {
        foreach (var n in _versionFromNamespace.Keys)
        {
            if (Array.IndexOf(namespaces, n) >= 0)
            {
                Version = _versionFromNamespace[n];
                return;
            }
        }

        Version = PackageVersion.Unknown;
    }

    public static UWPApplication[] All()
    {
        return All(out _);
    }

    internal static UWPApplication[] All(out bool isComplete)
    {
        var packages = CurrentUserPackages(out var packagesComplete);
        var apps = All(packages, out var manifestsComplete);
        isComplete = packagesComplete && manifestsComplete;
        return apps;
    }

    internal static UWPApplication[] All(
        IEnumerable<IPackage> packages,
        out bool isComplete,
        bool background = false,
        Action<IPackage, Exception>? onError = null,
        CancellationToken cancellationToken = default)
    {
        var appsBag = new ConcurrentBag<UWPApplication>();
        var incomplete = 0;

        void IndexPackage(IPackage p)
        {
            try
            {
                var u = new UWP(p);
                if (!u.TryInitializeAppInfo(p.InstalledLocation, error => onError?.Invoke(p, error)))
                {
                    System.Threading.Interlocked.Exchange(ref incomplete, 1);
                }

                foreach (var app in u.Apps)
                {
                    appsBag.Add(app);
                }
            }
            catch (Exception ex)
            {
                System.Threading.Interlocked.Exchange(ref incomplete, 1);
                onError?.Invoke(p, ex);
                Logger.LogError(ex.Message);
            }
        }

        if (background)
        {
            foreach (var package in packages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IndexPackage(package);
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
                IndexPackage);
        }

        isComplete = incomplete == 0;
        return appsBag.ToArray();
    }

    internal static IEnumerable<IPackage> CurrentUserPackages(out bool isComplete)
    {
        isComplete = true;
        var currentUsersPackages = PackageManagerWrapper.FindPackagesForCurrentUser();
        ICollection<IPackage> packagesToReturn = [];

        foreach (var pkg in currentUsersPackages)
        {
            try
            {
                var f = pkg.IsFramework;
                var path = pkg.InstalledLocation;

                if (!f && !string.IsNullOrEmpty(path))
                {
                    packagesToReturn.Add(pkg);
                }
            }
            catch (Exception ex)
            {
                isComplete = false;
                Logger.LogError(ex.Message);
            }
        }

        return packagesToReturn;
    }

    public override string ToString()
    {
        return FamilyName;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1309:Use ordinal string comparison", Justification = "Using CurrentCultureIgnoreCase since this is used with FamilyName")]
    public override bool Equals(object? obj)
    {
        if (obj is UWP uwp)
        {
            // Using CurrentCultureIgnoreCase since this is used with FamilyName
            return FamilyName.Equals(uwp.FamilyName, StringComparison.CurrentCultureIgnoreCase);
        }
        else
        {
            return false;
        }
    }

    public override int GetHashCode()
    {
        // Using CurrentCultureIgnoreCase since this is used with FamilyName
        return FamilyName.GetHashCode(StringComparison.CurrentCultureIgnoreCase);
    }

    public enum PackageVersion
    {
        Windows10,
        Windows81,
        Windows8,
        Unknown,
    }
}
