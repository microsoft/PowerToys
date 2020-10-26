// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Security.Principal;
using Windows.Management.Deployment;
using Wox.Plugin.Logger;
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "We want to catch all exception to prevent error in a program from affecting loading of program plugin.")]
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
                    try
                    {
                        packages.Add(PackageWrapper.GetWrapperFromPackage(p));
                    }
                    catch (Exception e)
                    {
                        Log.Error(e.Message, GetType());
                    }
                }
            }

            return packages;
        }
    }
}
