// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace ManagedCommon
{
    [SupportedOSPlatform("windows")]
    public class PowerToysPathResolver
    {
        private const string PowerToysRegistryKey = @"Software\Classes\powertoys";
        private const string PowerToysExe = "PowerToys.exe";

        /// <summary>
        /// Gets the PowerToys installation path by checking registry entries
        /// </summary>
        /// <returns>The path to PowerToys installation directory, or null if not found</returns>
        public static string GetPowerToysInstallPath()
        {
#if DEBUG
            // In debug builds, resolve directly from the running process (no installer/registry involved).
            return GetPathFromCurrentProcess();
#else
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
#endif
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

        private static string GetPathFromCurrentProcess()
        {
            try
            {
                // If we're running inside PowerToys.exe (dev/debug builds), use the executable location.
                var processPath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(processPath))
                {
                    var processDir = Path.GetDirectoryName(processPath);
                    if (!string.IsNullOrEmpty(processDir) && File.Exists(Path.Combine(processDir, PowerToysExe)))
                    {
                        return processDir;
                    }
                }

                // As a fallback, walk up from AppContext.BaseDirectory to find PowerToys.exe.
                var directory = new DirectoryInfo(AppContext.BaseDirectory);
                while (directory != null)
                {
                    var candidate = Path.Combine(directory.FullName, PowerToysExe);
                    if (File.Exists(candidate))
                    {
                        return directory.FullName;
                    }

                    directory = directory.Parent;
                }
            }
            catch
            {
                // Ignore reflection/process permission errors; caller will see null and handle accordingly.
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
