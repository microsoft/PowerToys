using Microsoft.Plugin.Program.Logger;
using Microsoft.Plugin.Program.Programs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Wox.Infrastructure.Storage;

namespace Microsoft.Plugin.Program.Storage
{
    /// <summary>
    /// A repository for storing packaged applications such as UWP apps or appx packaged desktop apps.
    /// This repository will also monitor for changes to the PackageCatelog and update the repository accordingly
    /// </summary>
    internal class PackageRepository : ListRepository<UWP.Application>, IRepository<UWP.Application>, IProgramRepository
    {
        IPackageCatalog _packageCatalog;
        public PackageRepository(IPackageCatalog packageCatalog, IStorage<IList<UWP.Application>> storage) : base(storage)
        {
            _packageCatalog = packageCatalog ?? throw new ArgumentNullException("packageCatalog", "PackageRepository expects an interface to be able to subscribe to package events");
            _packageCatalog.PackageInstalling += OnPackageInstalling;
            _packageCatalog.PackageUninstalling += OnPackageUninstalling;
        }

        public void OnPackageInstalling(PackageCatalog p, PackageInstallingEventArgs args)
        {
            if (args.IsComplete)
            {

                try
                {
                    var uwp = new UWP(args.Package);
                    uwp.InitializeAppInfo(args.Package.InstalledLocation.Path);
                    foreach (var app in uwp.Apps)
                    {
                        Add(app);
                    }
                }
                //InitializeAppInfo will throw if there is no AppxManifest.xml for the package. 
                //Note there are sometimes multiple packages per application and this doesn't necessarily mean that we haven't found the app.
                //eg. "Could not find file 'C:\\Program Files\\WindowsApps\\Microsoft.WindowsTerminalPreview_2020.616.45.0_neutral_~_8wekyb3d8bbwe\\AppxManifest.xml'."

                catch (System.IO.FileNotFoundException e)
                {
                    ProgramLogger.LogException($"|UWP|OnPackageInstalling|{args.Package.InstalledLocation}|{e.Message}", e);
                }
            }
        }

        public void OnPackageUninstalling(PackageCatalog p, PackageUninstallingEventArgs args)
        {
            if (args.Progress == 0)
            {
                //find apps associated with this package. 
                var uwp = new UWP(args.Package);
                var apps = _items.Where(a => a.Package.Equals(uwp)).ToArray();
                foreach (var app in apps)
                {
                    Remove(app);
                }
            }
        }

        public void IndexPrograms()
        {
            var windows10 = new Version(10, 0);
            var support = Environment.OSVersion.Version.Major >= windows10.Major;

            var applications = support ? Programs.UWP.All() : new Programs.UWP.Application[] { };
            Set(applications);
        }

        public void Save()
        {
            _storage.Save(_items);
        }

        public void Load()
        {
            var items = _storage.TryLoad(new Programs.UWP.Application[] { });
            Set(items);
        }
    }
}
