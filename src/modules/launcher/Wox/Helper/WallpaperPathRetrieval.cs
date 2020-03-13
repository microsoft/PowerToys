using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using Microsoft.Win32;

namespace Wox.Helper
{
    public static class WallpaperPathRetrieval
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern Int32 SystemParametersInfo(UInt32 action,
            Int32 uParam, StringBuilder vParam, UInt32 winIni);
        private static readonly UInt32 SPI_GETDESKWALLPAPER = 0x73;
        private static int MAX_PATH = 260;

        public static string GetWallpaperPath()
        {
            var wallpaper = new StringBuilder(MAX_PATH);
            SystemParametersInfo(SPI_GETDESKWALLPAPER, MAX_PATH, wallpaper, 0);

            var str = wallpaper.ToString();
            if (string.IsNullOrEmpty(str))
                return null;

            return str;
        }

        public static Color GetWallpaperColor()
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey("Control Panel\\Colors", true);
            var result = key.GetValue(@"Background", null);
            if (result != null && result is string)
            {
                try
                {
                    var parts = result.ToString().Trim().Split(new[] {' '}, 3).Select(byte.Parse).ToList();
                    return Color.FromRgb(parts[0], parts[1], parts[2]);
                }
                catch
                {
                }
            }
            return Colors.Transparent;
        }
    }
}
