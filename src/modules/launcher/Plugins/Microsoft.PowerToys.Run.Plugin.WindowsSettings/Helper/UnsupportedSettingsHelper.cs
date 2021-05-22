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

            var currentBuild = GetCurrentWindowsRegistryValue("CurrentBuild");
            var currentBuildNumber = GetCurrentWindowsRegistryValue("CurrentBuildNumber");

            if (currentBuild != currentBuildNumber)
            {
                Log.Warn(
                    $"Registry value 'CurrentBuild'={currentBuild} differ from Registry value 'CurrentBuildNumber'={currentBuildNumber}",
                    typeof(UnsupportedSettingsHelper));
            }

            var currentWindowsBuild = currentBuild != uint.MinValue
                ? currentBuild
                : currentBuildNumber;

            var filteredSettingsList = settingsList.Where(found
                => (found.DeprecatedInBuild == null || currentWindowsBuild < found.DeprecatedInBuild)
                && (found.IntroducedInBuild == null || currentWindowsBuild >= found.IntroducedInBuild));

            filteredSettingsList = filteredSettingsList.OrderBy(found => found.Name);

            return filteredSettingsList;
        }

        /// <summary>
        /// Return a numeric value from the registry key
        /// "HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows NT\CurrentVersion"
        /// </summary>
        /// <param name="registryValueName">The name of the registry value.</param>
        /// <returns>A registry value or <see cref="uint.MinValue"/> on error.</returns>
        private static uint GetCurrentWindowsRegistryValue(in string registryValueName)
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

                return uint.MinValue;
            }

            return uint.TryParse(registryValueData as string, out var buildNumber)
                ? buildNumber
                : uint.MinValue;
        }
    }
}
