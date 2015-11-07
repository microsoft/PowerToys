using System;
using System.Collections.Generic;
using Wox.Infrastructure.Logger;

namespace Wox.Plugin.Program.ProgramSources
{
    [Serializable]
    [System.ComponentModel.Browsable(false)]
    public class AppPathsProgramSource : AbstractProgramSource
    {
        public AppPathsProgramSource()
        {
            BonusPoints = -10;
        }

        public AppPathsProgramSource(ProgramSource source) : this()
        {
            BonusPoints = source.BonusPoints;
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
                    try
                    {
                        using (var key = root.OpenSubKey(item))
                        {
                            string path = key.GetValue("") as string;
                            if (path == null) continue;

                            // fix path like this ""\"C:\\folder\\executable.exe\"""
                            const int begin = 0;
                            int end = path.Length - 1;
                            const char quotationMark = '"';
                            if (path[begin] == quotationMark && path[end] == quotationMark)
                            {
                                path = path.Substring(begin + 1, path.Length - 2);
                            }

                            if (!System.IO.File.Exists(path)) continue;
                            var entry = CreateEntry(path);
                            entry.ExecuteName = item;
                            list.Add(entry);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
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
