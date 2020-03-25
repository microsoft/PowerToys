using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Wox.Infrastructure
{
    public static class Constant
    {
        public const string Wox = "Wox";
        public const string Plugins = "Plugins";

        private static readonly Assembly Assembly = Assembly.GetExecutingAssembly();
        public static readonly string ProgramDirectory = Directory.GetParent(Assembly.Location.NonNull()).ToString();
        public static readonly string ExecutablePath = Path.Combine(ProgramDirectory, Wox + ".exe");

        public static bool IsPortableMode;
        public const string PortableFolderName = "UserData";
        public static string PortableDataPath = Path.Combine(ProgramDirectory, PortableFolderName);
        public static string DetermineDataDirectory()
        {
            if (Directory.Exists(PortableDataPath))
            {
                IsPortableMode = true;
                return PortableDataPath;
            }
            else
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Wox);
            }
        }

        public static readonly string DataDirectory = DetermineDataDirectory();
        public static readonly string PluginsDirectory = Path.Combine(DataDirectory, Plugins);
        public static readonly string PreinstalledDirectory = Path.Combine(ProgramDirectory, Plugins);
        public const string Issue = "https://github.com/Wox-launcher/Wox/issues/new";
        public static readonly string Version = FileVersionInfo.GetVersionInfo(Assembly.Location.NonNull()).ProductVersion;

        public static readonly int ThumbnailSize = 64;
        public static readonly string DefaultIcon = Path.Combine(ProgramDirectory, "Images", "app.png");
        public static readonly string ErrorIcon = Path.Combine(ProgramDirectory, "Images", "app_error.png");

        public static string EverythingSDKPath;
    }
}
