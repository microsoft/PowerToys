// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.WindowsSettings.Helper
{
    /// <summary>
    /// Helper class to easier work with registry entries
    /// </summary>
    internal static class RegistryHelper
    {
        /// <summary>
        /// Return the current version of the Windows OS (e.g. 2004)
        /// </summary>
        /// <returns>The current version of the Windows OS.</returns>
        internal static ushort GetCurrentWindowsVersion()
        {
            object releaseId;

            try
            {
                releaseId = Win32.Registry.GetValue(
                    "HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows NT\\CurrentVersion",
                    "ReleaseId",
                    null);
            }
            catch (Exception exception)
            {
                Log.Exception("Can't get registry value", exception, typeof(RegistryHelper));

                // fall-back
                return ushort.MinValue;
            }

            return ushort.TryParse(releaseId as string, out var currentWindowsVersion)
                ? currentWindowsVersion
                : ushort.MinValue;
        }
    }
}
