// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace WorkspacesCsharpLibrary
{
    public class PwaHelper
    {
        private const string ChromeBase = "Google\\Chrome\\User Data\\Default\\Web Applications";
        private const string EdgeBase = "Microsoft\\Edge\\User Data\\Default\\Web Applications";
        private const string ResourcesDir = "Manifest Resources";
        private const string IconsDir = "Icons";
        private static readonly Lazy<PwaHelper> Default = new(() => new PwaHelper());
        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();
        private readonly ConcurrentDictionary<string, string> _icons = new(StringComparer.OrdinalIgnoreCase);
        private readonly string _localAppData;

        public PwaHelper()
            : this(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
        {
        }

        internal PwaHelper(string localAppData)
        {
            _localAppData = localAppData;
        }

        public static string GetPwaIconFilename(string pwaAppId) =>
            string.IsNullOrEmpty(pwaAppId) ? string.Empty : Default.Value.FindIcon(pwaAppId);

        internal string FindIcon(string appId)
        {
            if (string.IsNullOrEmpty(appId) || appId.IndexOfAny(InvalidFileNameChars) >= 0 || appId.EndsWith('.') || appId.EndsWith(' '))
            {
                return string.Empty;
            }

            if (_icons.TryGetValue(appId, out string cached) && File.Exists(cached))
            {
                return cached;
            }

            var icon = FindBrowserIcon(EdgeBase, appId) ?? FindBrowserIcon(ChromeBase, appId);
            if (icon != null)
            {
                _icons[appId] = icon;
            }

            return icon ?? string.Empty;
        }

        private string FindBrowserIcon(string browserFolder, string appId)
        {
            string root = Path.Combine(_localAppData, browserFolder);
            return FirstImage(Path.Combine(root, "_crx_" + appId), "*.ico") ??
                   FirstImage(Path.Combine(root, "_crx__" + appId), "*.ico") ??
                   FirstImage(Path.Combine(root, ResourcesDir, appId, IconsDir), "*.png");
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
