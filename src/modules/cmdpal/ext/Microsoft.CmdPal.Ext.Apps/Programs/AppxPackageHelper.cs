// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Runtime.InteropServices;
using Windows.Win32.System.Com;
using static Microsoft.CmdPal.Ext.Apps.Utils.Native;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

public static class AppxPackageHelper
{
    private static readonly IAppxFactory AppxFactory = (IAppxFactory)new AppxFactory();

    // This function returns a list of attributes of applications
    internal static IEnumerable<IAppxManifestApplication> GetAppsFromManifest(IStream stream)
    {
        var reader = AppxFactory.CreateManifestReader(stream);
        var manifestApps = reader.GetApplications();

        while (manifestApps.GetHasCurrent())
        {
            var manifestApp = manifestApps.GetCurrent();
            var hr = manifestApp.GetStringValue("AppListEntry", out var appListEntry);
            _ = CheckHRAndReturnOrThrow(hr, appListEntry);
            if (appListEntry != "none")
            {
                yield return manifestApp;
            }

            manifestApps.MoveNext();
        }
    }

    internal static T CheckHRAndReturnOrThrow<T>(HRESULT hr, T result)
    {
        if (hr != HRESULT.S_OK)
        {
            Marshal.ThrowExceptionForHR((int)hr);
        }

        return result;
    }
}
