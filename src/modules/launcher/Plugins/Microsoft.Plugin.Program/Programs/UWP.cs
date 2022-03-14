// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Xml.Linq;
using Microsoft.Plugin.Program.Logger;
using Wox.Plugin.Common.Win32;
using Wox.Plugin.Logger;

namespace Microsoft.Plugin.Program.Programs
{
    [Serializable]
    public partial class UWP
    {
        private static readonly IPath Path = new FileSystem().Path;

        private static readonly Dictionary<string, PackageVersion> _versionFromNamespace = new Dictionary<string, PackageVersion>
        {
            { "http://schemas.microsoft.com/appx/manifest/foundation/windows10", PackageVersion.Windows10 },
            { "http://schemas.microsoft.com/appx/2013/manifest", PackageVersion.Windows81 },
            { "http://schemas.microsoft.com/appx/2010/manifest", PackageVersion.Windows8 },
        };

        public string Name { get; }

        public string FullName { get; }

        public string FamilyName { get; }

        public string Location { get; set; }

        public IList<UWPApplication> Apps { get; private set; }

        public PackageVersion Version { get; set; }

        public static IPackageManager PackageManagerWrapper { get; set; } = new PackageManagerWrapper();

        public UWP(IPackage package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            Name = package.Name;
            FullName = package.FullName;
            FamilyName = package.FamilyName;
        }

        public void InitializeAppInfo(string installedLocation)
        {
            Location = installedLocation;
            var path = Path.Combine(installedLocation, "AppxManifest.xml");

            var namespaces = XmlNamespaces(path);
            InitPackageVersion(namespaces);

            const uint noAttribute = 0x80;
            const STGM exclusiveRead = STGM.READ;
            var hResult = NativeMethods.SHCreateStreamOnFileEx(path, exclusiveRead, noAttribute, false, null, out IStream stream);

            if (hResult == HRESULT.S_OK)
            {
                Apps = AppxPackageHelper.GetAppsFromManifest(stream).Select(appInManifest => new UWPApplication(appInManifest, this)).Where(a =>
                {
                    var valid =
                    !string.IsNullOrEmpty(a.UserModelId) &&
                    !string.IsNullOrEmpty(a.DisplayName) &&
                    a.AppListEntry != "none";

                    return valid;
                }).ToList();

                if (Marshal.ReleaseComObject(stream) > 0)
                {
                    Log.Error("AppxManifest.xml was leaked", MethodBase.GetCurrentMethod().DeclaringType);
                }
            }
            else
            {
                var e = Marshal.GetExceptionForHR((int)hResult);
                ProgramLogger.Exception("Error caused while trying to get the details of the UWP program", e, GetType(), path);

                Apps = Array.Empty<UWPApplication>();
            }
        }

        // http://www.hanselman.com/blog/GetNamespacesFromAnXMLDocumentWithXPathDocumentAndLINQToXML.aspx
        private static string[] XmlNamespaces(string path)
        {
            XDocument z = XDocument.Load(path);
            if (z.Root != null)
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
                Log.Error($"Error occurred while trying to get the XML from {path}", MethodBase.GetCurrentMethod().DeclaringType);

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

            ProgramLogger.Exception($"|Trying to get the package version of the UWP program, but a unknown UWP appmanifest version {FullName} from location {Location} is returned.", new FormatException(), GetType(), Location);

            Version = PackageVersion.Unknown;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Intentionally keeping the process alive.")]
        public static UWPApplication[] All()
        {
            var windows10 = new Version(10, 0);
            var support = Environment.OSVersion.Version.Major >= windows10.Major;
            if (support)
            {
                var applications = CurrentUserPackages().AsParallel().SelectMany(p =>
                {
                    UWP u;
                    try
                    {
                        u = new UWP(p);
                        u.InitializeAppInfo(p.InstalledLocation);
                    }
                    catch (Exception e)
                    {
                        ProgramLogger.Exception($"Unable to convert Package to UWP for {p.FullName}", e, MethodBase.GetCurrentMethod().DeclaringType, p.InstalledLocation);

                        return Array.Empty<UWPApplication>();
                    }

                    return u.Apps;
                });

                var updatedListWithoutDisabledApps = applications
                                                        .Where(t1 => Main.Settings.DisabledProgramSources.All(x => x.UniqueIdentifier != t1.UniqueIdentifier))
                                                        .Select(x => x);

                return updatedListWithoutDisabledApps.ToArray();
            }
            else
            {
                return Array.Empty<UWPApplication>();
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Intentionally keeping the process alive.")]
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
                catch (Exception e)
                {
                    ProgramLogger.Exception("An unexpected error occurred and unable to verify if package is valid", e, MethodBase.GetCurrentMethod().DeclaringType, "id");
                    return false;
                }
            });
        }

        public override string ToString()
        {
            return FamilyName;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1309:Use ordinal string comparison", Justification = "Using CurrentCultureIgnoreCase since this is used with FamilyName")]
        public override bool Equals(object obj)
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
}
