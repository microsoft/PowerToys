using System.Collections.Generic;
using Wox.Infrastructure.Storage.UserSettings;

namespace Wox.Plugin.SystemPlugins.Program.ProgramSources
{
    [global::System.ComponentModel.Browsable(false)]
    public class AppPathsProgramSource: AbstractProgramSource
    {
        public AppPathsProgramSource()
        {
            this.BonusPoints = -10;
        }

        public AppPathsProgramSource(ProgramSource source)
            : this()
        {
            this.BonusPoints = source.BonusPoints;
        }

        public override List<Program> LoadPrograms()
        {
            var list = new List<Program>();
            ReadAppPaths(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths", list);
            ReadAppPaths(@"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\App Paths", list); //TODO: need test more on 64-bit
            return list;
        }

        private void ReadAppPaths(string rootpath, List<Program> list)
        {
            using (var root = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(rootpath))
            {
                if (root == null) return;
                foreach (var item in root.GetSubKeyNames())
                {
                    using (var key = root.OpenSubKey(item))
                    {
                        object path = key.GetValue("");
                        if (path is string && global::System.IO.File.Exists((string)path))
                        {
                            var entry = CreateEntry((string)path);
                            entry.ExecuteName = item;
                            list.Add(entry);
                        }

                        key.Close();
                    }
                }
            }
        }

        public override string ToString()
        {
            return typeof(AppPathsProgramSource).Name;
        }
    }
}
