using System;
using System.IO;
using System.Reflection;

namespace Wox.Infrastructure
{
    public static class Wox
    {
        public const string Name = "Wox";
        public const string Plugins = "Plugins";
        public const string Settings = "Settings";

        public static readonly string ProgramPath = Directory.GetParent(Assembly.GetExecutingAssembly().Location).ToString();
        public static readonly string DataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Name);
        public static readonly string UserDirectory = Path.Combine(DataPath, Plugins);
        public static readonly string PreinstalledDirectory = Path.Combine(ProgramPath, Plugins);
        public static readonly string SettingsPath = Path.Combine(DataPath, Settings);
        public const string Github = "https://github.com/Wox-launcher/Wox";
    }
}
