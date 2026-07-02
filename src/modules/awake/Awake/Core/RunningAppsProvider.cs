// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Awake.Core.Models;
using ManagedCommon;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Awake.Core
{
    /// <summary>
    /// Enumerates running applications for the flyout's "While app runs" picker. The default list
    /// (<see cref="GetRunningAppsAsync"/>) is limited to apps with a visible main window; searching
    /// widens the net to all processes (<see cref="GetAllProcessesAsync"/>). Enumeration and icon
    /// extraction are intended to run off the UI thread; the per-item XAML icon is built from the
    /// captured PNG bytes on the UI thread via <see cref="BuildIconAsync"/>.
    /// </summary>
    internal static class RunningAppsProvider
    {
        internal static Task<List<RunningAppInfo>> GetRunningAppsAsync() => Task.Run(() => GetRunningApps(windowedOnly: true));

        internal static Task<List<RunningAppInfo>> GetAllProcessesAsync() => Task.Run(() => GetRunningApps(windowedOnly: false));

        private static List<RunningAppInfo> GetRunningApps(bool windowedOnly)
        {
            var apps = new List<RunningAppInfo>();
            var seen = new HashSet<int>();
            var iconCache = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
            int ownPid = Environment.ProcessId;

            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    string title = process.MainWindowTitle;
                    bool hasWindow = process.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(title);

                    // The default list only surfaces user-facing apps (a real main window with a
                    // non-empty title); search widens to every accessible process.
                    if (windowedOnly && !hasWindow)
                    {
                        continue;
                    }

                    if (process.Id == ownPid || !seen.Add(process.Id))
                    {
                        continue;
                    }

                    string executablePath = TryGetExecutablePath(process);

                    apps.Add(new RunningAppInfo
                    {
                        ProcessId = process.Id,
                        DisplayName = GetFriendlyName(process, executablePath),
                        WindowTitle = hasWindow ? title : string.Empty,
                        IconBytes = GetIconPngCached(executablePath, iconCache),
                    });
                }
                catch (Exception ex)
                {
                    // Inaccessible/elevated/system processes throw on property access; skip them.
                    Logger.LogInfo($"Skipping process during enumeration: {ex.Message}");
                }
                finally
                {
                    process.Dispose();
                }
            }

            return apps
                .OrderBy(a => a.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static string TryGetExecutablePath(Process process)
        {
            try
            {
                return process.MainModule?.FileName ?? string.Empty;
            }
            catch
            {
                // MainModule throws for processes we cannot fully open; no path available.
                return string.Empty;
            }
        }

        private static string GetFriendlyName(Process process, string executablePath)
        {
            if (!string.IsNullOrEmpty(executablePath))
            {
                try
                {
                    string description = FileVersionInfo.GetVersionInfo(executablePath).FileDescription ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        return description;
                    }
                }
                catch
                {
                    // Fall through to the process name.
                }
            }

            return process.ProcessName;
        }

        private static byte[]? GetIconPngCached(string executablePath, Dictionary<string, byte[]?> cache)
        {
            if (string.IsNullOrEmpty(executablePath))
            {
                return null;
            }

            if (cache.TryGetValue(executablePath, out byte[]? cached))
            {
                return cached;
            }

            byte[]? png = TryGetIconPng(executablePath);
            cache[executablePath] = png;
            return png;
        }

        private static byte[]? TryGetIconPng(string executablePath)
        {
            if (string.IsNullOrEmpty(executablePath))
            {
                return null;
            }

            try
            {
                using Icon? icon = Icon.ExtractAssociatedIcon(executablePath);
                if (icon == null)
                {
                    return null;
                }

                using Bitmap bitmap = icon.ToBitmap();
                using var stream = new MemoryStream();
                bitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Builds a XAML <see cref="BitmapImage"/> from captured PNG bytes. Must be called on the
        /// UI thread (creates a XAML object).
        /// </summary>
        internal static async Task<BitmapImage?> BuildIconAsync(byte[]? iconBytes)
        {
            if (iconBytes == null || iconBytes.Length == 0)
            {
                return null;
            }

            try
            {
                var image = new BitmapImage();
                using var stream = new MemoryStream(iconBytes);
                await image.SetSourceAsync(stream.AsRandomAccessStream());
                return image;
            }
            catch (Exception ex)
            {
                Logger.LogInfo($"Failed to build app icon: {ex.Message}");
                return null;
            }
        }
    }
}
