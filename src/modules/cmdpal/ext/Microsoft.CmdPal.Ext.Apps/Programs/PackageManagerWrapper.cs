// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Security.Principal;
using Windows.Management.Deployment;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

public class PackageManagerWrapper : IPackageManager
{
    private readonly PackageManager _packageManager;

    public PackageManagerWrapper()
    {
        _packageManager = new PackageManager();
    }

    public IEnumerable<IPackage> FindPackagesForCurrentUser()
    {
        var user = WindowsIdentity.GetCurrent().User;

        if (user is not null)
        {
            var pkgs = _packageManager.FindPackagesForUser(user.Value);

            ICollection<IPackage> packages = [];

            foreach (var package in pkgs)
            {
                if (package is not null)
                {
                    packages.Add(PackageWrapper.GetWrapperFromPackage(package));
                }
            }

            return packages;
        }

        return [];
    }
}
