// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.WindowsSettings.Helper
{
    /// <summary>
    /// Helper class to easier work with the version of the Windows OS
    /// </summary>
    internal static class UnsupportedSettingsHelper
    {
        private const string _keyPath = "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion";
        private const string _keyNameBuild = "CurrentBuild";
        private const string _keyNameBuildNumber = "CurrentBuildNumber";

        /// <summary>
        /// Remove all <see cref="WindowsSetting"/> from the settings list in the given <see cref="WindowsSetting"/> class.
        /// </summary>
        /// <param name="windowsSettings">A class that contain all possible windows settings.</param>
        internal static void FilterByBuild(in WindowsSettings windowsSettings)
        {
            if (windowsSettings?.Settings is null)
            {
                return;
            }

            var currentBuild = GetNumericRegistryValue(_keyPath, _keyNameBuild);
            var currentBuildNumber = GetNumericRegistryValue(_keyPath, _keyNameBuildNumber);

            if (currentBuild != currentBuildNumber)
            {
                var usedValueName = currentBuild != uint.MinValue ? _keyNameBuild : _keyNameBuildNumber;
                var warningMessage =
                    $"Detecting the Windows version in registry ({_keyPath}) leads to an inconclusive"
                    + $" result ({_keyNameBuild}={currentBuild}, {_keyNameBuildNumber}={currentBuildNumber})!"
                    + $" For resolving the conflict we use the value of '{usedValueName}'.";

                Log.Warn(warningMessage, typeof(UnsupportedSettingsHelper));
            }

            var currentWindowsBuild = currentBuild != uint.MinValue
                ? currentBuild
                : currentBuildNumber;

            var filteredSettingsList = windowsSettings.Settings.Where(found
                => (found.DeprecatedInBuild == null || currentWindowsBuild < found.DeprecatedInBuild)
                && (found.IntroducedInBuild == null || currentWindowsBuild >= found.IntroducedInBuild));

            filteredSettingsList = filteredSettingsList.OrderBy(found => found.Name);

            windowsSettings.Settings = filteredSettingsList;
        }

        /// <summary>
        /// Return a unsigned numeric value from given registry value name inside the given registry key.
        /// </summary>
        /// <param name="registryKey">The registry key.</param>
        /// <param name="valueName">The name of the registry value.</param>
        /// <returns>A registry value or <see cref="uint.MinValue"/> on error.</returns>
        private static uint GetNumericRegistryValue(in string registryKey, in string valueName)
        {
            object? registryValueData;

            try
            {
                registryValueData = Win32.Registry.GetValue(registryKey, valueName, uint.MinValue);
            }
            catch (Exception exception)
            {
                Log.Exception(
                    $"Can't get registry value for '{valueName}'",
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
