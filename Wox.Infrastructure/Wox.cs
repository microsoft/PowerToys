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
        public const string Settings = "Settings";

        private static readonly Assembly Assembly = Assembly.GetExecutingAssembly();
        public static readonly string ProgramDirectory = Directory.GetParent(Assembly.Location).ToString();
        public static readonly string ExecutablePath = Path.Combine(ProgramDirectory, Wox + ".exe");
        public static readonly string DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Wox);
        public static readonly string UserDirectory = Path.Combine(DataDirectory, Plugins);
        public static readonly string PreinstalledDirectory = Path.Combine(ProgramDirectory, Plugins);
        public static readonly string SettingsPath = Path.Combine(DataDirectory, Settings);
        public const string Github = "https://github.com/Wox-launcher/Wox";
        public static readonly string Version = FileVersionInfo.GetVersionInfo(Assembly.Location).ProductVersion;
    }
}
