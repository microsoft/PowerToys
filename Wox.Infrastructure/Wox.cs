using System;
using System.IO;
using System.Reflection;

namespace Wox.Infrastructure
{
    public static class Wox
    {
        public const string Name = "Wox";
        public static readonly string ProgramPath = Directory.GetParent(Assembly.GetExecutingAssembly().Location).ToString();
        public static readonly string DataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Name);
    }
}
