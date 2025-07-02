// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.Win32;

namespace ManagedCommon
{
    public class PowerToysPathResolver
    {
        private const string PowerToysRegistryKey = @"Software\Classes\powertoys";
        private const string PowerToysUserRegistryKey = @"Software\Microsoft\PowerToys";
        private const string PowerToysExe = "PowerToys.exe";

        /// <summary>
        /// Gets the PowerToys installation path by checking registry entries
        /// </summary>
        /// <returns>The path to PowerToys installation directory, or null if not found</returns>
        public static string GetPowerToysInstallPath()
        {
            // Try to get path from Per-User installation first
            string path = GetPathFromRegistry(RegistryHive.CurrentUser);
            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }

            // Fall back to Per-Machine installation
            path = GetPathFromRegistry(RegistryHive.LocalMachine);
            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }

            return null;
        }

        /// <summary>
        /// Gets the PowerToys executable path
        /// </summary>
        /// <returns>The full path to PowerToys.exe, or null if not found</returns>
        public static string GetPowerToysExecutablePath()
        {
            string installPath = GetPowerToysInstallPath();
            if (string.IsNullOrEmpty(installPath))
            {
                return null;
            }

            string exePath = Path.Combine(installPath, PowerToysExe);
            return File.Exists(exePath) ? exePath : null;
        }

        /// <summary>
        /// Checks if PowerToys is installed for the current user
        /// </summary>
        /// <returns>True if PowerToys is installed for the current user</returns>
        public static bool IsPowerToysInstalled()
        {
            return !string.IsNullOrEmpty(GetPowerToysInstallPath());
        }

        /// <summary>
        /// Gets the installation scope (perUser or perMachine)
        /// </summary>
        /// <returns>The installation scope, or null if not found</returns>
        public static string GetInstallationScope()
        {
            // Check Per-User first
            using (var key = Registry.CurrentUser.OpenSubKey(PowerToysRegistryKey))
            {
                if (key != null)
                {
                    return key.GetValue("InstallScope")?.ToString();
                }
            }

            // Check Per-Machine
            using (var key = Registry.LocalMachine.OpenSubKey(PowerToysRegistryKey))
            {
                if (key != null)
                {
                    return key.GetValue("InstallScope")?.ToString();
                }
            }

            return null;
        }

        private static string GetPathFromRegistry(RegistryHive hive)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);

                // First try to get path from the powertoys protocol registration
                string path = GetPathFromProtocolRegistration(baseKey);
                if (!string.IsNullOrEmpty(path))
                {
                    return path;
                }

                // For user installations, also check the user-specific key
                if (hive == RegistryHive.CurrentUser)
                {
                    path = GetPathFromUserRegistration(baseKey);
                    if (!string.IsNullOrEmpty(path))
                    {
                        return path;
                    }
                }
            }
            catch (Exception)
            {
                // Ignore registry access errors
            }

            return null;
        }

        private static string GetPathFromProtocolRegistration(RegistryKey baseKey)
        {
            try
            {
                using var key = baseKey.OpenSubKey($@"{PowerToysRegistryKey}\shell\open\command");

                if (key != null)
                {
                    string command = key.GetValue(string.Empty)?.ToString();
                    if (!string.IsNullOrEmpty(command))
                    {
                        // Parse command like: "C:\Program Files\PowerToys\PowerToys.exe" "%1"
                        return ExtractPathFromCommand(command);
                    }
                }
            }
            catch (Exception)
            {
                // Ignore registry access errors
            }

            return null;
        }

        private static string GetPathFromUserRegistration(RegistryKey baseKey)
        {
            try
            {
                using var key = baseKey.OpenSubKey(PowerToysUserRegistryKey);

                if (key != null)
                {
                    var installed = key.GetValue("installed");
                    if (installed != null && installed.ToString() == "1")
                    {
                        // User registry key exists but doesn't contain path
                        // Try to get path from protocol registration
                        return GetPathFromProtocolRegistration(baseKey);
                    }
                }
            }
            catch (Exception)
            {
                // Ignore registry access errors
            }

            return null;
        }

        private static string ExtractPathFromCommand(string command)
        {
            if (string.IsNullOrEmpty(command))
            {
                return null;
            }

            try
            {
                // Handle quoted paths: "C:\Program Files\PowerToys\PowerToys.exe" "%1"
                if (command.StartsWith('\"'))
                {
                    int endQuote = command.IndexOf('\"', 1);
                    if (endQuote > 1)
                    {
                        string exePath = command.Substring(1, endQuote - 1);
                        if (File.Exists(exePath))
                        {
                            return Path.GetDirectoryName(exePath);
                        }
                    }
                }
                else
                {
                    // Handle unquoted paths (less common)
                    string[] parts = command.Split(' ');
                    if (parts.Length > 0 && File.Exists(parts[0]))
                    {
                        return Path.GetDirectoryName(parts[0]);
                    }
                }
            }
            catch (Exception)
            {
                // Ignore path parsing errors
            }

            return null;
        }
    }
}
