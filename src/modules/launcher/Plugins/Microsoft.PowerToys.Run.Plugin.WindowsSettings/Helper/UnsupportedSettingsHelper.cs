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
    internal static class UnsupportedSettingsHelper
    {
        /// <summary>
        /// Remove all <see cref="WindowsSetting"/> of the given list that are not present on the current used Windows build.
        /// </summary>
        /// <param name="settingsList">The list with <see cref="WindowsSetting"/> to filter.</param>
        /// <returns>A new list with <see cref="WindowsSetting"/> that only contain present Windows settings for this OS.</returns>
        internal static IEnumerable<WindowsSetting> FilterByBuild(in IEnumerable<WindowsSetting>? settingsList)
        {
            if (settingsList is null)
            {
                return Enumerable.Empty<WindowsSetting>();
            }

            var currentWindowsBuild = GetCurrentWindowsRegistryValue("CurrentBuild");
            if (currentWindowsBuild == uint.MinValue)
            {
                currentWindowsBuild = GetCurrentWindowsRegistryValue("CurrentBuildNumber");
            }

            // remove deprecated settings and settings that are for a higher Windows builds
            var filteredSettingsList = settingsList.Where(found
                => (found.DeprecatedInBuild == null || currentWindowsBuild < found.DeprecatedInBuild)
                && (found.IntroducedInBuild == null || currentWindowsBuild >= found.IntroducedInBuild));

            // sort settings list
            filteredSettingsList = filteredSettingsList.OrderBy(found => found.Name);

            return filteredSettingsList;
        }

        /// <summary>
        /// Return a registry value of the current Windows OS
        /// </summary>
        /// <param name="registryValueName">The name of the registry value that contains the build number.</param>
        /// <returns>a registry value.</returns>
        private static uint GetCurrentWindowsRegistryValue(string registryValueName)
        {
            object registryValueData;

            try
            {
                registryValueData = Win32.Registry.GetValue(
                    "HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows NT\\CurrentVersion",
                    registryValueName,
                    null);
            }
            catch (Exception exception)
            {
                Log.Exception(
                    $"Can't get registry value for '{registryValueName}'",
                    exception,
                    typeof(UnsupportedSettingsHelper));

                // fall-back
                return ushort.MinValue;
            }

            return ushort.TryParse(registryValueData as string, out var buildNumber)
                ? buildNumber
                : ushort.MinValue;
        }
    }
}
