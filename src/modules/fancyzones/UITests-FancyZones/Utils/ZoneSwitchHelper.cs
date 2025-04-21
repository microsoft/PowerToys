// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.FancyZones.UITests.Utils
{
    public class ZoneSwitchHelper
    {
        public static void LaunchExplorer(string path)
        {
            var explorerProcessInfo = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = path,
            };

            Process.Start(explorerProcessInfo);
            Task.Delay(2000).Wait(); // Wait for the Explorer window to fully launch
        }

        public static string? GetZoneIndexSetByAppName(string exeName, string json)
        {
            if (string.IsNullOrEmpty(exeName) || string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                var historyArray = doc.RootElement.GetProperty("app-zone-history");

                foreach (var item in historyArray.EnumerateArray())
                {
                    if (item.TryGetProperty("app-path", out var appPathElement) &&
                        appPathElement.GetString() is string path &&
                        path.EndsWith(exeName, StringComparison.OrdinalIgnoreCase))
                    {
                        var history = item.GetProperty("history");
                        if (history.GetArrayLength() > 0)
                        {
                            return history[0].GetProperty("zone-index-set")[0].GetString();
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("JSON parse error: " + ex.Message, ex);
            }

            return null;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        public static IntPtr GetForegroundWindowp()
        {
            return GetForegroundWindow();
        }

        public static string? GetActiveWindowTitle()
        {
            const int nChars = 256;
            StringBuilder buff = new StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();

            if (GetWindowText(handle, buff, nChars) > 0)
            {
                return buff.ToString();
            }

            return null;
        }

        public static void KillAllExplorerWindows()
        {
            foreach (var process in Process.GetProcessesByName("explorer"))
            {
                try
                {
                    process.Kill();
                    process.WaitForExit();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to kill explorer.exe (PID: {process.Id}): {ex.Message}");
                }
            }
        }
    }
}
