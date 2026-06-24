// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.Win32;

namespace PowerToysExtension.Helpers;

/// <summary>
/// Helper methods for locating the installed PowerToys binaries.
/// </summary>
internal static class PowerToysPathResolver
{
    private const string PowerToysProtocolKey = @"Software\Classes\powertoys";
    private const string PowerToysUserKey = @"Software\Microsoft\PowerToys";

    internal static string GetPowerToysInstallPath()
    {
        var perUser = GetInstallPathFromRegistry(RegistryHive.CurrentUser);
        if (!string.IsNullOrEmpty(perUser))
        {
            return perUser;
        }

        return GetInstallPathFromRegistry(RegistryHive.LocalMachine);
    }

    internal static string TryResolveExecutable(string executableName)
    {
        if (string.IsNullOrEmpty(executableName))
        {
            return string.Empty;
        }

        var baseDirectory = GetPowerToysInstallPath();
        if (string.IsNullOrEmpty(baseDirectory))
        {
            return string.Empty;
        }

        var candidate = Path.Combine(baseDirectory, executableName);
        return File.Exists(candidate) ? candidate : string.Empty;
    }

    private static string GetInstallPathFromRegistry(RegistryHive hive)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);

            var protocolPath = GetPathFromProtocolRegistration(baseKey);
            if (!string.IsNullOrEmpty(protocolPath))
            {
                return protocolPath;
            }

            if (hive == RegistryHive.CurrentUser)
            {
                var userPath = GetPathFromUserRegistration(baseKey);
                if (!string.IsNullOrEmpty(userPath))
                {
                    return userPath;
                }
            }
        }
        catch
        {
            // Ignore registry access failures and fall back to other checks.
        }

        return string.Empty;
    }

    private static string GetPathFromProtocolRegistration(RegistryKey baseKey)
    {
        try
        {
            using var commandKey = baseKey.OpenSubKey($@"{PowerToysProtocolKey}\shell\open\command");
            if (commandKey == null)
            {
                return string.Empty;
            }

            var command = commandKey.GetValue(string.Empty)?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(command))
            {
                return string.Empty;
            }

            return ExtractInstallDirectory(command);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetPathFromUserRegistration(RegistryKey baseKey)
    {
        try
        {
            using var userKey = baseKey.OpenSubKey(PowerToysUserKey);
            if (userKey == null)
            {
                return string.Empty;
            }

            var installedValue = userKey.GetValue("installed");
            if (installedValue != null && installedValue.ToString() == "1")
            {
                return GetPathFromProtocolRegistration(baseKey);
            }
        }
        catch
        {
            // Ignore registry access failures.
        }

        return string.Empty;
    }

    private static string ExtractInstallDirectory(string command)
    {
        if (string.IsNullOrEmpty(command))
        {
            return string.Empty;
        }

        try
        {
            if (command.StartsWith('"'))
            {
                var closingQuote = command.IndexOf('"', 1);
                if (closingQuote > 1)
                {
                    var quotedPath = command.Substring(1, closingQuote - 1);
                    if (File.Exists(quotedPath))
                    {
                        return Path.GetDirectoryName(quotedPath) ?? string.Empty;
                    }
                }
            }
            else
            {
                var parts = command.Split(' ');
                if (parts.Length > 0 && File.Exists(parts[0]))
                {
                    return Path.GetDirectoryName(parts[0]) ?? string.Empty;
                }
            }
        }
        catch
        {
            // Fall through and report no path.
        }

        return string.Empty;
    }
}
