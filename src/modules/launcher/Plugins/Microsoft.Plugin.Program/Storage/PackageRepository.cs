// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.Plugin.Program.Logger;
using Microsoft.Plugin.Program.Programs;
using Windows.ApplicationModel;
using Wox.Infrastructure.Storage;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Microsoft.Plugin.Program.Storage
{
    /// <summary>
    /// A repository for storing packaged applications such as UWP apps or appx packaged desktop apps.
    /// This repository will also monitor for changes to the PackageCatalog and update the repository accordingly
    /// </summary>
    internal class PackageRepository : ListRepository<UWPApplication>, IProgramRepository
    {
        private readonly IPackageCatalog _packageCatalog;
        private readonly PluginInitContext _context;

        public PackageRepository(IPackageCatalog packageCatalog, PluginInitContext context)
        {
            _packageCatalog = packageCatalog ?? throw new ArgumentNullException(nameof(packageCatalog), "PackageRepository expects an interface to be able to subscribe to package events");
            _context = context ?? throw new ArgumentNullException(nameof(context));

            _packageCatalog.PackageInstalling += OnPackageInstalling;
            _packageCatalog.PackageUninstalling += OnPackageUninstalling;
            _packageCatalog.PackageUpdating += OnPackageUpdating;
        }

        public void OnPackageInstalling(PackageCatalog p, PackageInstallingEventArgs args)
        {
            if (args.IsComplete)
            {
                AddPackage(args.Package);
            }
        }

        public void OnPackageUninstalling(PackageCatalog p, PackageUninstallingEventArgs args)
        {
            if (args.Progress == 0)
            {
                RemovePackage(args.Package);
            }
        }

        public void OnPackageUpdating(PackageCatalog p, PackageUpdatingEventArgs args)
        {
            if (args.Progress == 0)
            {
                RemovePackage(args.SourcePackage);
            }

            if (args.IsComplete)
            {
                AddPackage(args.TargetPackage);
            }
        }

        private void AddPackage(Package package)
        {
            var packageWrapper = PackageWrapper.GetWrapperFromPackage(package);
            if (string.IsNullOrEmpty(packageWrapper.InstalledLocation))
            {
                return;
            }

            try
            {
                var uwp = new UWP(packageWrapper);
                uwp.InitializeAppInfo(packageWrapper.InstalledLocation);
                foreach (var app in uwp.Apps)
                {
                    app.UpdateLogoPath(_context.API.GetCurrentTheme());
                    Add(app);
                }
            }

            // InitializeAppInfo will throw if there is no AppxManifest.xml for the package.
            // Note there are sometimes multiple packages per product and this doesn't necessarily mean that we haven't found the app.
            // eg. "Could not find file 'C:\\Program Files\\WindowsApps\\Microsoft.WindowsTerminalPreview_2020.616.45.0_neutral_~_8wekyb3d8bbwe\\AppxManifest.xml'."
            catch (System.IO.FileNotFoundException e)
            {
                ProgramLogger.Exception(e.Message, e, GetType(), package.InstalledLocation.ToString());
            }
        }

        private void RemovePackage(Package package)
        {
            // find apps associated with this package.
            var packageWrapper = PackageWrapper.GetWrapperFromPackage(package);
            var uwp = new UWP(packageWrapper);
            var apps = Items.Where(a => a.Package.Equals(uwp)).ToArray();

            foreach (var app in apps)
            {
                Remove(app);
            }
        }

        public void IndexPrograms()
        {
            var windows10 = new Version(10, 0);
            var support = Environment.OSVersion.Version.Major >= windows10.Major;

            var applications = support ? Programs.UWP.All() : Array.Empty<UWPApplication>();
            Log.Info($"Indexed {applications.Length} packaged applications", GetType());
            SetList(applications);
        }
    }
}
