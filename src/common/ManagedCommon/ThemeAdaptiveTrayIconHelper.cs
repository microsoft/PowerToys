// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ManagedCommon
{
    // Loads white/dark tray icons based on system theme, matching runner/tray_icon.cpp get_icon().
    public static class ThemeAdaptiveTrayIconHelper
    {
        private const uint LoadImageIcon = 1;
        private const uint LrLoadFromFile = 0x00000010;
        private const uint LrDefaultSize = 0x00000040;

        public static IntPtr LoadIconHandle(
            bool themeAdaptive,
            string whiteIconPath,
            string darkIconPath,
            Func<IntPtr> fallbackLoadIcon)
        {
            if (themeAdaptive)
            {
                var iconPath = ThemeHelpers.GetSystemTheme() == AppTheme.Dark ? whiteIconPath : darkIconPath;
                if (File.Exists(iconPath))
                {
                    var icon = LoadImage(IntPtr.Zero, iconPath, LoadImageIcon, 0, 0, LrLoadFromFile | LrDefaultSize);
                    if (icon != IntPtr.Zero)
                    {
                        return icon;
                    }
                }
            }

            return fallbackLoadIcon();
        }

        public static void DestroyIconHandle(IntPtr iconHandle)
        {
            if (iconHandle != IntPtr.Zero)
            {
                DestroyIcon(iconHandle);
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadImage(
            IntPtr hInst,
            string name,
            uint type,
            int cx,
            int cy,
            uint fuLoad);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}
