// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Update
{
    public static class UpdateUtilities
    {
        public static async void UninstallPreviousMsixVersions()
        {
            try
            {
                Windows.Management.Deployment.PackageManager packageManager = new();
                var packages = packageManager.FindPackagesForUser(string.Empty, "Microsoft.PowerToys", "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US");

                Version? currentVersion = Assembly.GetExecutingAssembly().GetName().Version;

                if (currentVersion == null)
                {
                    return;
                }

                foreach (var package in packages)
                {
                    Version msixVersion = new(package.Id.Version.Major, package.Id.Version.Minor, package.Id.Version.Revision);
                    if (msixVersion < currentVersion)
                    {
                        await packageManager.RemovePackageAsync(package.Id.FullName);
                    }
                }
            }
            catch
            {
            }
        }
    }
}
