// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
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

        if (user != null)
        {
            var pkgs = _packageManager.FindPackagesForUser(user.Value);

            return pkgs.Select(PackageWrapper.GetWrapperFromPackage).Where(package => package != null);
        }

        return Enumerable.Empty<IPackage>();
    }
}
