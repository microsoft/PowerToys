// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using static Microsoft.Plugin.Program.Programs.UWP;

namespace Microsoft.Plugin.Program.Programs
{
    public static class AppxPackageHelper
    {
        // This function returns a list of attributes of applications
        public static List<IAppxManifestApplication> GetAppsFromManifest(IStream stream)
        {
            List<IAppxManifestApplication> apps = new List<IAppxManifestApplication>();
            var appxFactory = new AppxFactory();
            var reader = ((IAppxFactory)appxFactory).CreateManifestReader(stream);
            var manifestApps = reader.GetApplications();

            while (manifestApps.GetHasCurrent())
            {
                var manifestApp = manifestApps.GetCurrent();
                var hr = manifestApp.GetStringValue("AppListEntry", out var appListEntry);
                _ = CheckHRAndReturnOrThrow(hr, appListEntry);
                if (appListEntry != "none")
                {
                    apps.Add(manifestApp);
                }

                manifestApps.MoveNext();
            }

            return apps;
        }

        public static T CheckHRAndReturnOrThrow<T>(Hresult hr, T result)
        {
            if (hr != Hresult.Ok)
            {
                Marshal.ThrowExceptionForHR((int)hr);
            }

            return result;
        }
    }
}
