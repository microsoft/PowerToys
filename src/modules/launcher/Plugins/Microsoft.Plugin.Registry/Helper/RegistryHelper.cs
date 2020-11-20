// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Microsoft.Win32;

namespace Microsoft.Plugin.Registry.Helper
{
    #pragma warning disable CA1031 // Do not catch general exception types

    internal static class RegistryHelper
    {
        private static readonly IReadOnlyDictionary<string, RegistryKey?> _mainKeys = new Dictionary<string, RegistryKey?>(13)
        {
            { "HKEY_", null },
            { "HKCR", Win32.Registry.ClassesRoot },
            { Win32.Registry.ClassesRoot.Name, Win32.Registry.ClassesRoot },
            { "HKCC", Win32.Registry.CurrentConfig },
            { Win32.Registry.CurrentConfig.Name, Win32.Registry.CurrentConfig },
            { "HKCU", Win32.Registry.CurrentUser },
            { Win32.Registry.CurrentUser.Name, Win32.Registry.CurrentUser },
            { "HKLM", Win32.Registry.LocalMachine },
            { Win32.Registry.LocalMachine.Name, Win32.Registry.LocalMachine },
            { "HKPD", Win32.Registry.PerformanceData },
            { Win32.Registry.PerformanceData.Name, Win32.Registry.PerformanceData },
            { "HKU", Win32.Registry.Users },
            { Win32.Registry.Users.Name, Win32.Registry.Users },
        };

        /// <summary>
        /// Try to find a registry key based on the given query
        /// </summary>
        /// <param name="query">The query to search</param>
        /// <returns>A combination of the main <see cref="RegistryKey"/> and the sub keys</returns>
        internal static (RegistryKey? mainKey, string subKey) GetRegistryKey(in string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return (null, "Search is empty");
            }

            var mainKey = query.Split('\\').FirstOrDefault();
            var subKey = query.TrimStart('\\').Replace(mainKey, string.Empty, StringComparison.InvariantCultureIgnoreCase);
            var mainKeyResult = _mainKeys.FirstOrDefault(found => found.Key.StartsWith(mainKey, StringComparison.InvariantCultureIgnoreCase));

            return (mainKeyResult.Value, subKey);
        }

        /// <summary>
        /// Return a list of all registry main key
        /// </summary>
        /// <returns>A list with key-value-pairs of all registry main key and possible exceptions</returns>
        internal static ICollection<(string, RegistryKey?, Exception?)> GetAllMainKeys()
            => new Collection<(string, RegistryKey?, Exception?)>
            {
                (Win32.Registry.ClassesRoot.Name, null, null),
                (Win32.Registry.CurrentConfig.Name, null, null),
                (Win32.Registry.CurrentUser.Name, null, null),
                (Win32.Registry.LocalMachine.Name, null, null),
                (Win32.Registry.PerformanceData.Name, null, null),
                (Win32.Registry.Users.Name, null, null),
            };

        /// <summary>
        /// Search for the given sub-key path in the given main registry key
        /// </summary>
        /// <param name="mainKey">The main <see cref="RegistryKey"/></param>
        /// <param name="subKeyPath">The path of the registry sub-key</param>
        /// <returns>A list with key-value-pairs that contain the sub-key and possible exceptions</returns>
        internal static ICollection<(string, RegistryKey?, Exception?)> SearchForSubKey(in RegistryKey mainKey, in string subKeyPath)
        {
            Debug.WriteLine($"Search for {mainKey.Name}\\{subKeyPath}\n");

            var subKeysNames = subKeyPath.Split('\\');
            var index = 0;
            var subKey = mainKey;

            ICollection<(string, RegistryKey?, Exception?)> result;

            do
            {
                var subKeyName = subKeysNames.ElementAtOrDefault(index);
                result = FindSubKey(subKey, subKeyName);

                if (result.Count == 0)
                {
                    return GetAllSubKeys(mainKey);
                }

                if (result.Count == 1 && index < subKeysNames.Length)
                {
                    subKey = result.First().Item2;
                }

                if (subKey == null)
                {
                    break;
                }

                index++;
            }
            while (index < subKeysNames.Length);

            return result;
        }

        /// <summary>
        /// Return a human readable summary of a given <see cref="RegistryKey"/>
        /// </summary>
        /// <param name="key">The <see cref="RegistryKey"/> for the summary</param>
        /// <returns>A human readable summary</returns>
        internal static string GetSummary(in RegistryKey key)
            => $"Sub-keys: {key.SubKeyCount} - Values: {key.ValueCount}";

        /// <summary>
        /// Open a given registry key in the registry editor (build-in in Windows)
        /// </summary>
        /// <param name="fullKey">The registry key to open</param>
        internal static void OpenRegistryKey(in string fullKey)
        {
            // it's impossible to direct open a key via command-line option, so we must override the last remember key
            Win32.Registry.SetValue(@"HKEY_Current_User\Software\Microsoft\Windows\CurrentVersion\Applets\Regedit", "LastKey", fullKey);

            Process.Start(new ProcessStartInfo
            {
                Arguments = "-m",           // -m => allow multi-instance (hidden start option)
                FileName = "regedit.exe",
                Verb = "runas",             // Start as Administraor
                UseShellExecute = true,     // Start as administraor will not work without this
            });
        }

        /// <summary>
        /// Try to find the given registry sub-key in the given registry parent-key
        /// </summary>
        /// <param name="parentKey">The parent-key, also the root to start the search</param>
        /// <param name="searchSubKey">The sub-key to find</param>
        /// <returns>A list with key-value-pairs that contain the sub-key and possible exceptions</returns>
        private static ICollection<(string, RegistryKey?, Exception?)> FindSubKey(in RegistryKey parentKey, in string searchSubKey)
        {
            var list = new Collection<(string, RegistryKey?, Exception?)>();

            Debug.WriteLine($"Search for {searchSubKey} in {parentKey.Name}");

            try
            {
                foreach (var subKey in parentKey.GetSubKeyNames())
                {
                    if (subKey.StartsWith(searchSubKey, StringComparison.InvariantCultureIgnoreCase))
                    {
                        try
                        {
                            list.Add(($"{parentKey.Name}\\{subKey}", parentKey.OpenSubKey(subKey), null));
                        }
                        catch (Exception exception)
                        {
                            list.Add(($"{parentKey.Name}\\{subKey}", null, exception));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                list.Add((parentKey.Name, null, ex));
            }

            return list;
        }

        /// <summary>
        /// Return a list with a registry sub-keys of the given registry parent-key
        /// </summary>
        /// <param name="parentKey">The registry parent-key</param>
        /// <param name="maxCount">(optional) The maximum count of the results</param>
        /// <returns>A list with key-value-pairs that contain the sub-key and possible exceptions</returns>
        private static ICollection<(string, RegistryKey?, Exception?)> GetAllSubKeys(in RegistryKey parentKey, in int maxCount = 50)
        {
            var list = new Collection<(string, RegistryKey?, Exception?)>();

            try
            {
                foreach (var subKey in parentKey.GetSubKeyNames())
                {
                    list.Add(($"{parentKey.Name}\\{subKey}", parentKey, null));

                    if (list.Count > maxCount)
                    {
                        break;
                    }
                }
            }
            catch (Exception exception)
            {
                list.Add(($"{parentKey.Name}", null, exception));
            }

            return list;
        }
    }

    #pragma warning restore CA1031 // Do not catch general exception types
}
