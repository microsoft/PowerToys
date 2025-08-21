// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
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

            Apps = AppxPackageHelper.GetAppsFromManifest(stream).Select(appInManifest =>
            {
                using var appHandle = new SafeComHandle(appInManifest);
                return new UWPApplication((IAppxManifestApplication*)appInManifest, this);
            }).Where(a =>
            {
                var valid =
                    !string.IsNullOrEmpty(a.UserModelId) &&
                    !string.IsNullOrEmpty(a.DisplayName) &&
                    a.AppListEntry != "none";
                return valid;
            }).ToList();
        }
        catch (Exception ex)
        {
            Apps = Array.Empty<UWPApplication>();
            Logger.LogError($"Failed to initialize UWP app info for {Name} ({FullName}): {ex.Message}");
            return;
        }
    }

    // http://www.hanselman.com/blog/GetNamespacesFromAnXMLDocumentWithXPathDocumentAndLINQToXML.aspx
    private static string[] XmlNamespaces(string path)
    {
        var z = XDocument.Load(path);
        if (z.Root is not null)
        {
            var namespaces = z.Root.Attributes().
                Where(a => a.IsNamespaceDeclaration).
                GroupBy(
                    a => a.Name.Namespace == XNamespace.None ? string.Empty : a.Name.LocalName,
                    a => XNamespace.Get(a.Value)).Select(
                    g => g.First().ToString()).ToArray();
            return namespaces;
        }
        else
        {
            return Array.Empty<string>();
        }
    }

    private void InitPackageVersion(string[] namespaces)
    {
        foreach (var n in _versionFromNamespace.Keys.Where(namespaces.Contains))
        {
            Version = _versionFromNamespace[n];
            return;
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
                    if (AllAppsSettings.Instance.DisabledProgramSources.All(x => x.UniqueIdentifier != app.UniqueIdentifier))
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
        return PackageManagerWrapper.FindPackagesForCurrentUser().Where(p =>
        {
            try
            {
                var f = p.IsFramework;
                var path = p.InstalledLocation;
                return !f && !string.IsNullOrEmpty(path);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
                return false;
            }
        });
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
