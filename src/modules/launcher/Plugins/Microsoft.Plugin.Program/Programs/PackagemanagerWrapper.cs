using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Security.Principal;
using Windows.Management.Deployment;
using Windows.ApplicationModel;
using Package = Windows.ApplicationModel.Package;

namespace Microsoft.Plugin.Program.Programs
{
    public class PackageManagerWrapper : IPackageManager
    {
        readonly PackageManager packageManager;

        public PackageManagerWrapper()
        {
            packageManager = new PackageManager();
        }

        public IEnumerable<IPackage> FindPackagesForCurrentUser()
        {
            List<PackageWrapper> packages = new List<PackageWrapper>();
            var user = WindowsIdentity.GetCurrent().User;

            if (user != null)
            {
                var id = user.Value;
                var m = this.packageManager.FindPackagesForUser(id);
                foreach (Package p in m)
                {
                    packages.Add(new PackageWrapper(
                        p.Id.Name,
                        p.Id.FullName, 
                        p.Id.FamilyName,
                        p.InstalledLocation.Path,
                        p.IsFramework));
                }
            }

            return packages;
        }
    }
}
