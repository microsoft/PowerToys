// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Internal;

namespace Microsoft.FancyZones.UITests.Utils
{
    public class ZoneSwitchHelper
    {
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
                            return history[0].GetProperty("zone-index-set")[0].GetRawText();
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

        public static string GetActiveWindowTitle()
        {
            const int nChars = 256;
            StringBuilder buff = new StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();

            if (GetWindowText(handle, buff, nChars) > 0)
            {
                return buff.ToString();
            }
            else
            {
                // Handle the error if needed
                throw new InvalidOperationException("Failed to get window title.");
            }
        }

        public static (int Dx, int Dy) GetOffset(Element element, int targetX, int targetY)
        {
            Assert.IsNotNull(element.Rect, "element is null");
            var rect = element.Rect.Value;
            return (targetX - rect.X, targetY - rect.Y);
        }

        public static (int X, int Y) GetScreenMargins((int Left, int Top, int Right, int Bottom) rect, int quantile = 4)
        {
            if (quantile == 0)
            {
                throw new ArgumentException("Quantile cannot be zero.", nameof(quantile));
            }

            int x = (rect.Left + rect.Right) / quantile;
            int y = (rect.Top + rect.Bottom) / quantile;
            return (x, y);
        }
    }
}
