// SilentInstallHelper.cs - Fix for Issue #29779
using System;
namespace PowerToys.Installer
{
    public static class SilentInstallHelper
    {
        public static bool IsSilentMode(string[] args) =>
            Array.Exists(args, a => a.Equals("/quiet", StringComparison.OrdinalIgnoreCase) ||
                                   a.Equals("/silent", StringComparison.OrdinalIgnoreCase) ||
                                   a.Equals("-q", StringComparison.OrdinalIgnoreCase));
    }
}
