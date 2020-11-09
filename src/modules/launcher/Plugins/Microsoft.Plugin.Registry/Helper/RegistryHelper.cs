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
        /// Try to find a registery key based on the given serach query
        /// </summary>
        /// <param name="query">The query to search</param>
        /// <returns>A combination of the main <see cref="RegistryKey"/> and the sub keys</returns>
        internal static (RegistryKey? mainKey, string subKey) GetRegisteryKey(in string query)
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
        /// Return a list of all registery main key
        /// </summary>
        /// <returns>A list with a key-value-pair of all registery main key and possible exceptions</returns>
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

        internal static ICollection<(string, RegistryKey?, Exception?)> SerachForKey(in RegistryKey mainKey, in string subKeyPath)
        {
            Debug.WriteLine($"Serarch for {mainKey.Name}\\{subKeyPath}\n");

            var subKeysNames = subKeyPath.Split('\\');
            var index = 0;
            var subKey = mainKey;

            ICollection<(string, RegistryKey?, Exception?)> result;

            do
            {
                var subKeyName = subKeysNames.ElementAtOrDefault(index);
                result = FindKey(subKey, subKeyName);

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

        internal static string GetKeyResult(in RegistryKey subKey)
            => $"Sub-keys: {subKey.SubKeyCount} - Values: {subKey.ValueCount}";

        internal static void OpenRegisteryKey(string fullKey)
        {
            // it's impossible to direct open a key via command-line option, so we must override the last remembert key
            Win32.Registry.SetValue(@"HKEY_Current_User\Software\Microsoft\Windows\CurrentVersion\Applets\Regedit", "LastKey", fullKey);

            // -m => allow multi-instance (hidden start option)
            Process.Start("regedit.exe", "-m");
        }

        private static ICollection<(string, RegistryKey?, Exception?)> FindKey(RegistryKey parentkey, string searchSubKey)
        {
            var list = new Collection<(string, RegistryKey?, Exception?)>();

            Debug.WriteLine($"Search for {searchSubKey} in {parentkey.Name}");

            try
            {
                foreach (var subKey in parentkey.GetSubKeyNames())
                {
                    if (subKey.StartsWith(searchSubKey, StringComparison.InvariantCultureIgnoreCase))
                    {
                        try
                        {
                            list.Add(($"{parentkey.Name}\\{subKey}", parentkey.OpenSubKey(subKey), null));
                        }
                        catch (Exception exception)
                        {
                            list.Add(($"{parentkey.Name}\\{subKey}", null, exception));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                list.Add((parentkey.Name, null, ex));
            }

            return list;
        }

        private static ICollection<(string, RegistryKey?, Exception?)> GetAllSubKeys(in RegistryKey key, in int maxCount = 50)
        {
            var list = new Collection<(string, RegistryKey?, Exception?)>();

            try
            {
                foreach (var subKey in key.GetSubKeyNames())
                {
                    list.Add(($"{key.Name}\\{subKey}", key, null));

                    if (list.Count > maxCount)
                    {
                        break;
                    }
                }
            }
            catch (Exception exception)
            {
                list.Add(($"{key.Name}", null, exception));
            }

            return list;
        }
    }

    #pragma warning restore CA1031 // Do not catch general exception types
}
