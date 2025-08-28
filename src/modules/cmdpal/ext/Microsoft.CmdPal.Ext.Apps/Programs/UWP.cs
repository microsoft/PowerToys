// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Abstractions;
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
    private static readonly IPath Path = new FileSystem().Path;

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

    public UWP(IPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        Name = package.Name;
        FullName = package.FullName;
        FamilyName = package.FamilyName;
    }

    public unsafe void InitializeAppInfo(string installedLocation)
    {
        Location = installedLocation;
        LocationLocalized = ShellLocalization.Instance.GetLocalizedPath(installedLocation);
        var path = Path.Combine(installedLocation, "AppxManifest.xml");

        var namespaces = XmlNamespaces(path);
        InitPackageVersion(namespaces);

        const uint noAttribute = 0x80;
        const uint STGMREAD = 0x00000000;
        try
        {
            IStream* stream = null;
            PInvoke.SHCreateStreamOnFileEx(path, STGMREAD, noAttribute, false, null, &stream).ThrowOnFailure();
            using var streamHandle = new SafeComHandle((IntPtr)stream);

            var appsInManifest = AppxPackageHelper.GetAppsFromManifest(stream);

            foreach (var appInManifest in appsInManifest)
            {
                using var appHandle = new SafeComHandle(appInManifest);
                var uwpApp = new UWPApplication((IAppxManifestApplication*)appInManifest, this);

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
            Apps = Array.Empty<UWPApplication>();
            Logger.LogError($"Failed to initialize UWP app info for {Name} ({FullName}): {ex.Message}");
            return;
        }
    }

    private static string[] XmlNamespaces(string path)
    {
        var z = XDocument.Load(path);
        if (z.Root is not null)
        {
            var namespaces = new List<string>();

            var attributes = z.Root.Attributes();
            foreach (var attribute in attributes)
            {
                if (attribute.IsNamespaceDeclaration)
                {
                    // Extract namespace
                    var key = attribute.Name.Namespace == XNamespace.None ? string.Empty : attribute.Name.LocalName;
                    XNamespace ns = XNamespace.Get(attribute.Value);

                    // Check if we already have a namespace with this key
                    var alreadyExists = false;
                    foreach (var existingNs in namespaces)
                    {
                        // If we find the namespace value already in our list, skip adding it again
                        if (existingNs == ns.ToString())
                        {
                            alreadyExists = true;
                            break;
                        }
                    }

                    // Add the first namespace found for each key (equivalent to .GroupBy().Select(g => g.First()))
                    if (!alreadyExists)
                    {
                        namespaces.Add(ns.ToString());
                    }
                }
            }

            return namespaces.ToArray();
        }
        else
        {
            return Array.Empty<string>();
        }
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
        var appsBag = new ConcurrentBag<UWPApplication>();

        Parallel.ForEach(CurrentUserPackages(), p =>
        {
            try
            {
                var u = new UWP(p);
                u.InitializeAppInfo(p.InstalledLocation);

                foreach (var app in u.Apps)
                {
                    var isDisabled = false;

                    foreach (var disabled in AllAppsSettings.Instance.DisabledProgramSources)
                    {
                        if (disabled.UniqueIdentifier == app.UniqueIdentifier)
                        {
                            isDisabled = true;
                            break;
                        }
                    }

                    if (!isDisabled)
                    {
                        appsBag.Add(app);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
            }
        });

        return appsBag.ToArray();
    }

    private static IEnumerable<IPackage> CurrentUserPackages()
    {
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
