// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace WorkspacesCsharpLibrary
{
    public class PwaHelper
    {
        private const string ChromeBase = @"Google\Chrome\User Data";
        private const string EdgeBase = @"Microsoft\Edge\User Data";
        private const string ResourcesDir = "Manifest Resources";
        private const string IconsDir = "Icons";
        private static readonly Lazy<PwaHelper> Default = new(() => new PwaHelper());
        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();
        private readonly ConcurrentDictionary<(string ProfileDirectory, string AppId), string> _icons = new();
        private readonly string _localAppData;

        public PwaHelper()
            : this(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
        {
        }

        internal PwaHelper(string localAppData)
        {
            _localAppData = localAppData;
        }

        public static string GetPwaIconFilename(string pwaAppId) => GetPwaIconFilename(pwaAppId, null, null);

        public static string GetPwaIconFilename(string pwaAppId, string browserPath, string appUserModelId) =>
            string.IsNullOrEmpty(pwaAppId) ? string.Empty : Default.Value.FindIcon(pwaAppId, browserPath, appUserModelId);

        internal string FindIcon(string appId, string browserPath, string appUserModelId)
        {
            if (string.IsNullOrEmpty(appId) || appId.IndexOfAny(InvalidFileNameChars) >= 0 || appId.EndsWith('.') || appId.EndsWith(' '))
            {
                return string.Empty;
            }

            string executable = Path.GetFileName(browserPath);
            bool edge = string.Equals(executable, "msedge.exe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(executable, "msedge_proxy.exe", StringComparison.OrdinalIgnoreCase);
            bool chrome = string.Equals(executable, "chrome.exe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(executable, "chrome_proxy.exe", StringComparison.OrdinalIgnoreCase);
            if (!edge && !chrome && !string.IsNullOrEmpty(executable))
            {
                return string.Empty;
            }

            if (!edge && !chrome && appUserModelId != null)
            {
                edge = appUserModelId.StartsWith("MSEdge.", StringComparison.OrdinalIgnoreCase);
                chrome = appUserModelId.StartsWith("Chrome.", StringComparison.OrdinalIgnoreCase);
            }

            if (!TryGetProfileId(appId, appUserModelId, out string profile))
            {
                return string.Empty;
            }

            string[] roots = edge ? [EdgeBase] : chrome ? [ChromeBase] : [EdgeBase, ChromeBase];
            string match = null;
            foreach (string relativeRoot in roots)
            {
                string userData = Path.Combine(_localAppData, relativeRoot);
                try
                {
                    IEnumerable<string> directories = profile == "Default"
                        ? new[] { Path.Combine(userData, "Default") }
                        : Directory.EnumerateDirectories(userData).Where(directory => profile == null ||
                            MatchesComponent(ProfileId(Path.GetFileName(directory)), profile, 3));
                    foreach (string directory in directories)
                    {
                        string icon = FindProfileIcon(directory, appId);
                        if (icon != null)
                        {
                            if (match != null)
                            {
                                return string.Empty;
                            }

                            match = icon;
                        }
                    }
                }
                catch (DirectoryNotFoundException)
                {
                    // The browser need not be installed.
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return string.Empty;
                }
            }

            return match ?? string.Empty;
        }

        private static bool TryGetProfileId(string appId, string appUserModelId, out string profileId)
        {
            const string WebAppMarker = "._crx_";
            profileId = null;
            int marker = appUserModelId?.IndexOf(WebAppMarker, StringComparison.OrdinalIgnoreCase) ?? -1;
            if (marker < 0)
            {
                return true;
            }

            string components = appUserModelId[(marker + 1)..];
            int separator = components.IndexOf('.');
            bool hasProfile = separator >= 0;
            profileId = hasProfile ? components[(separator + 1)..] : "Default";
            string appComponent = hasProfile ? components[..separator] : components;
            return profileId.Length != 0 && MatchesComponent("_crx_" + appId, appComponent, hasProfile ? 3 : 2);
        }

        // Chromium's Windows AUMID uses the sanitized user-data and profile directory names.
        private static string ProfileId(string profileName) =>
            "UserData." + string.Concat(profileName.Where(character => char.IsAsciiLetterOrDigit(character) || character == '.'));

        private static bool MatchesComponent(string original, string captured, int componentCount)
        {
            if (string.Equals(original, captured, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Chromium's ShellUtil::BuildAppUserModelId divides 64 characters among the components,
            // allowing for their separators. Overlong components keep their two ends.
            int limit = (64 - (componentCount - 1)) / componentCount;
            int first = limit / 2;
            int last = limit - first;
            return original.Length > limit && captured.Length == limit &&
                original.AsSpan(0, first).Equals(captured.AsSpan(0, first), StringComparison.OrdinalIgnoreCase) &&
                original.AsSpan(original.Length - last).Equals(captured.AsSpan(first), StringComparison.OrdinalIgnoreCase);
        }

        private string FindProfileIcon(string profileDirectory, string appId)
        {
            var key = (profileDirectory, appId);
            if (_icons.TryGetValue(key, out string cached) && File.Exists(cached))
            {
                return cached;
            }

            string root = Path.Combine(profileDirectory, "Web Applications");
            var icon = FirstImage(Path.Combine(root, "_crx_" + appId), "*.ico") ??
                       FirstImage(Path.Combine(root, "_crx__" + appId), "*.ico") ??
                       FirstImage(Path.Combine(root, ResourcesDir, appId, IconsDir), "*.png");
            if (icon != null)
            {
                _icons[key] = icon;
            }

            return icon;
        }

        private static string FirstImage(string folder, string pattern)
        {
            try
            {
                return Directory.Exists(folder) ? Directory.EnumerateFiles(folder, pattern).FirstOrDefault() : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        internal static bool HasLaunchTarget(string browserPath, string appUserModelId, string version, Func<string, bool> resolveShellItem = null)
        {
            // AppLauncher substitutes a sibling proxy for these two browser executables.
            string executable = Path.GetFileName(browserPath);
            string fallbackPath = executable is "msedge.exe" or "chrome.exe" ? Path.ChangeExtension(browserPath, null) + "_proxy.exe" : browserPath;
            if (File.Exists(fallbackPath))
            {
                return true;
            }

            if (string.IsNullOrEmpty(appUserModelId) ||
                !int.TryParse(version, NumberStyles.Integer, CultureInfo.InvariantCulture, out int snapshotVersion) ||
                snapshotVersion < 1)
            {
                return false;
            }

            return (resolveShellItem ?? CanResolveShellItem)(@"shell:AppsFolder\" + appUserModelId);
        }

        private static bool CanResolveShellItem(string shellPath)
        {
            int result = SHParseDisplayName(shellPath, IntPtr.Zero, out IntPtr itemIdList, 0, IntPtr.Zero);
            bool registered = result >= 0 && itemIdList != IntPtr.Zero;
            Marshal.FreeCoTaskMem(itemIdList);
            return registered;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int SHParseDisplayName(string name, IntPtr bindContext, out IntPtr itemIdList, uint attributesIn, IntPtr attributesOut);
    }
}
