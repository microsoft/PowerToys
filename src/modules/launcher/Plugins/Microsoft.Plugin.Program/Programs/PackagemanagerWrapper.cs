// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Security.Principal;
using Windows.Management.Deployment;
using Package = Windows.ApplicationModel.Package;

namespace Microsoft.Plugin.Program.Programs
{
    public class PackageManagerWrapper : IPackageManager
    {
        private readonly PackageManager _packageManager;

        public PackageManagerWrapper()
        {
            _packageManager = new PackageManager();
        }

        public IEnumerable<IPackage> FindPackagesForCurrentUser()
        {
            List<PackageWrapper> packages = new List<PackageWrapper>();
            var user = WindowsIdentity.GetCurrent().User;

            if (user != null)
            {
                var id = user.Value;
                var m = _packageManager.FindPackagesForUser(id);
                foreach (Package p in m)
                {
                    packages.Add(PackageWrapper.GetWrapperFromPackage(p));
                }
            }

            return packages;
        }
    }
}
