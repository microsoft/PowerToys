// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace Microsoft.CmdPal.Ext.Apps.Helpers;

internal static class UninstallRegistryAppLocator
{
    private static readonly string[] UninstallBaseKeys =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
    ];

    /// <summary>
    /// Tries to find install directory and a list of plausible main EXEs from an uninstall key
    /// (e.g. Inno Setup keys like "{guid}_is1").
    /// <paramref name="exeCandidates"/> may be empty if we couldn't pick any safe EXEs.
    /// </summary>
    /// <returns>
    /// Returns true if the uninstall key is found and an install directory is resolved.
    /// </returns>
    public static bool TryGetInstallInfo(
        string uninstallKeyName,
        out string? installDir,
        out IReadOnlyList<string> exeCandidates,
        string? expectedExeName = null)
    {
        installDir = null;
        exeCandidates = [];

        if (string.IsNullOrWhiteSpace(uninstallKeyName))
        {
            throw new ArgumentException("Key name must not be null or empty.", nameof(uninstallKeyName));
        }

        uninstallKeyName = uninstallKeyName.Trim();

        foreach (var baseKeyPath in UninstallBaseKeys)
        {
            // HKLM
            using (var key = Registry.LocalMachine.OpenSubKey($"{baseKeyPath}\\{uninstallKeyName}"))
            {
                if (TryFromUninstallKey(key, expectedExeName, out installDir, out exeCandidates))
                {
                    return true;
                }
            }

            // HKCU
            using (var key = Registry.CurrentUser.OpenSubKey($"{baseKeyPath}\\{uninstallKeyName}"))
            {
                if (TryFromUninstallKey(key, expectedExeName, out installDir, out exeCandidates))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryFromUninstallKey(
        RegistryKey? key,
        string? expectedExeName,
        out string? installDir,
        out IReadOnlyList<string> exeCandidates)
    {
        installDir = null;
        exeCandidates = [];

        if (key is null)
        {
            return false;
        }

        var location = (key.GetValue("InstallLocation") as string)?.Trim('"', ' ', '\t');
        if (string.IsNullOrEmpty(location))
        {
            location = (key.GetValue("Inno Setup: App Path") as string)?.Trim('"', ' ', '\t');
        }

        if (string.IsNullOrEmpty(location))
        {
            var uninstall = key.GetValue("UninstallString") as string;
            var uninsExe = ExtractFirstPath(uninstall);
            if (!string.IsNullOrEmpty(uninsExe))
            {
                var dir = Path.GetDirectoryName(uninsExe);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    location = dir;
                }
            }
        }

        if (string.IsNullOrEmpty(location) || !Directory.Exists(location))
        {
            return false;
        }

        installDir = location;

        // Collect safe EXE candidates; may be empty if ambiguous or only uninstall exes exist.
        exeCandidates = GetExeCandidates(location, expectedExeName);
        return true;
    }

    private static IReadOnlyList<string> GetExeCandidates(string root, string? expectedExeName)
    {
        // Look at root and a "bin" subfolder (very common pattern)
        var allExes = Directory.EnumerateFiles(root, "*.exe", SearchOption.TopDirectoryOnly)
            .Concat(GetBinExes(root))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (allExes.Length == 0)
        {
            return [];
        }

        var result = new List<string>();

        // 1) Exact match on expected exe name (if provided), ignoring case, and not uninstall/setup-like.
        if (!string.IsNullOrWhiteSpace(expectedExeName))
        {
            foreach (var exe in allExes)
            {
                if (string.Equals(Path.GetFileName(exe), expectedExeName, StringComparison.OrdinalIgnoreCase) &&
                    !LooksLikeUninstallerOrSetup(exe))
                {
                    result.Add(exe);
                }
            }
        }

        // 2) All other non-uninstall/setup exes
        foreach (var exe in allExes)
        {
            if (LooksLikeUninstallerOrSetup(exe))
            {
                continue;
            }

            // Skip ones already added as expectedExeName matches
            if (result.Contains(exe, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(exe);
        }

        // 3) We intentionally do NOT add uninstall/setup/update exes here.
        // If you ever want them, you can add a separate API to expose them.
        return result;
    }

    private static IEnumerable<string> GetBinExes(string root)
    {
        var bin = Path.Combine(root, "bin");
        return !Directory.Exists(bin)
            ? []
            : Directory.EnumerateFiles(bin, "*.exe", SearchOption.TopDirectoryOnly);
    }

    private static bool LooksLikeUninstallerOrSetup(string path)
    {
        var name = Path.GetFileName(path);
        return name.StartsWith("unins", StringComparison.OrdinalIgnoreCase) // e.g. Inno: unins000.exe
               || name.Contains("setup", StringComparison.OrdinalIgnoreCase) // setup.exe
               || name.Contains("installer", StringComparison.OrdinalIgnoreCase) // installer.exe / MyAppInstaller.exe
               || name.Contains("update", StringComparison.OrdinalIgnoreCase); // updater/updater.exe
    }

    private static string? ExtractFirstPath(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return null;
        }

        commandLine = commandLine.Trim();

        if (commandLine.StartsWith('"'))
        {
            var endQuote = commandLine.IndexOf('"', 1);
            if (endQuote > 1)
            {
                return commandLine[1..endQuote];
            }
        }

        var firstSpace = commandLine.IndexOf(' ');
        var candidate = firstSpace > 0 ? commandLine[..firstSpace] : commandLine;
        candidate = candidate.Trim('"');
        return candidate.Length > 0 ? candidate : null;
    }
}
