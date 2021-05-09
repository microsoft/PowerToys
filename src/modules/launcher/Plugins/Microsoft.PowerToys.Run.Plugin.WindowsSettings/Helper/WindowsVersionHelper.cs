// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.WindowsSettings.Helper
{
    /// <summary>
    /// Helper class to easier work with the version of the Windows OS
    /// </summary>
    internal static class WindowsVersionHelper
    {
        /// <summary>
        /// Remove all <see cref="WindowsSetting"/> of the given list that are not present on the current used Windows version.
        /// </summary>
        /// <param name="settingsList">The list with <see cref="WindowsSetting"/> to filter.</param>
        /// <returns>A new list with <see cref="WindowsSetting"/> that only contain present Windows settings for this OS.</returns>
        internal static IEnumerable<WindowsSetting> FilterByVersion(in IEnumerable<WindowsSetting>? settingsList)
        {
            if (settingsList is null)
            {
                return Enumerable.Empty<WindowsSetting>();
            }

            var currentWindowsVersion = GetCurrentWindowsVersion();

            // remove deprecated settings and settings that are for a higher Windows versions
            var filteredSettingsList = settingsList.Where(found
                => (found.DeprecatedInVersion == null || currentWindowsVersion < found.DeprecatedInVersion)
                && (found.IntroducedInVersion == null || currentWindowsVersion >= found.IntroducedInVersion));

            // sort settings list
            filteredSettingsList = filteredSettingsList.OrderBy(found => found.Name);

            return filteredSettingsList;
        }

        /// <summary>
        /// Return the current version of the Windows OS (e.g. 2004)
        /// </summary>
        /// <returns>The current version of the Windows OS.</returns>
        private static ushort GetCurrentWindowsVersion()
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
                Log.Exception("Can't get registry value", exception, typeof(WindowsVersionHelper));

                // fall-back
                return ushort.MinValue;
            }

            return ushort.TryParse(releaseId as string, out var currentWindowsVersion)
                ? currentWindowsVersion
                : ushort.MinValue;
        }
    }
}
