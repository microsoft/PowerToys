// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Windows.Win32;
using Windows.Win32.Storage.Packaging.Appx;
using Windows.Win32.System.Com;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

public static class AppxPackageHelper
{
    internal static unsafe List<IntPtr> GetAppsFromManifest(IStream* stream)
    {
        PInvoke.CoCreateInstance(typeof(AppxFactory).GUID, null, CLSCTX.CLSCTX_INPROC_SERVER, out IAppxFactory* appxFactory).ThrowOnFailure();
        using var handle = new SafeComHandle((IntPtr)appxFactory);

        IAppxManifestReader* reader = null;
        IAppxManifestApplicationsEnumerator* manifestApps = null;
        var result = new List<IntPtr>();

        appxFactory->CreateManifestReader(stream, &reader);
        using var readerHandle = new SafeComHandle((IntPtr)reader);
        reader->GetApplications(&manifestApps);
        using var manifestAppsHandle = new SafeComHandle((IntPtr)manifestApps);

        while (true)
        {
            manifestApps->GetHasCurrent(out var hasCurrent);
            if (hasCurrent == false)
            {
                break;
            }

            IAppxManifestApplication* manifestApp = null;

            try
            {
                manifestApps->GetCurrent(&manifestApp).ThrowOnFailure();

                var hr = manifestApp->GetStringValue("AppListEntry", out var appListEntryPtr);
                var appListEntry = ComFreeHelper.GetStringAndFree(hr, appListEntryPtr);

                if (appListEntry != "none")
                {
                    result.Add((IntPtr)manifestApp);
                }
                else if (manifestApp is not null)
                {
                    manifestApp->Release();
                }
            }
            catch (Exception ex)
            {
                if (manifestApp is not null)
                {
                    manifestApp->Release();
                }

                Logger.LogError($"Failed to get current application from manifest: {ex.Message}");
            }

            manifestApps->MoveNext(out var hasNext);
            if (hasNext == false)
            {
                break;
            }
        }

        return result;
    }
}
