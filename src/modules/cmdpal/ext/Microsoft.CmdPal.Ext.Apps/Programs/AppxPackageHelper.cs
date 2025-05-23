// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.UI.Xaml.Controls;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.Packaging.Appx;
using Windows.Win32.System.Com;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

public static class AppxPackageHelper
{
    internal static unsafe List<IntPtr> GetAppsFromManifest(IStream* stream)
    {
        PInvoke.CoCreateInstance(typeof(AppxFactory).GUID, null, CLSCTX.CLSCTX_INPROC_SERVER, out IAppxFactory* appxFactory).ThrowOnFailure();

        IAppxManifestReader* reader = null;
        IAppxManifestApplicationsEnumerator* manifestApps = null;
        var result = new List<IntPtr>();

        try
        {
            appxFactory->CreateManifestReader(stream, &reader);
            reader->GetApplications(&manifestApps);

            while (true)
            {
                manifestApps->GetHasCurrent(out var hasCurrent);
                if (hasCurrent == false)
                {
                    break;
                }

                IAppxManifestApplication* manifestApp;
                manifestApps->GetCurrent(&manifestApp);

                manifestApp->GetStringValue("AppListEntry", out var appListEntryPtr).ThrowOnFailure();
                var appListEntry = appListEntryPtr.ToString();

                if (appListEntry != "none")
                {
                    result.Add((IntPtr)manifestApp);
                }

                manifestApps->MoveNext(out var hasNext);
                if (hasNext == false)
                {
                    break;
                }
            }
        }
        finally
        {
            ComFreeHelper.ComObjectRelease(appxFactory);
            ComFreeHelper.ComObjectRelease(reader);
            ComFreeHelper.ComObjectRelease(manifestApps);
        }

        return result;
    }
}
