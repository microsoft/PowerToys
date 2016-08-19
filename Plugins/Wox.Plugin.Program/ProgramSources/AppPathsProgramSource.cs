using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using Wox.Infrastructure.Logger;

namespace Wox.Plugin.Program.ProgramSources
{
    [Serializable]
    public class AppPathsProgramSource : ProgramSource
    {
        public override List<Program> LoadPrograms()
        {
            var list = new List<Program>();
            ReadAppPaths(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths", list);
            ReadAppPaths(@"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\App Paths", list);
                //TODO: need test more on 64-bit
            return list;
        }

        private void ReadAppPaths(string rootpath, List<Program> list)
        {
            using (var root = Registry.LocalMachine.OpenSubKey(rootpath))
            {
                if (root == null) return;
                foreach (var item in root.GetSubKeyNames())
                {
                    try
                    {
                        using (var key = root.OpenSubKey(item))
                        {
                            var path = key.GetValue("") as string;
                            if (string.IsNullOrEmpty(path)) continue;

                            // fix path like this ""\"C:\\folder\\executable.exe\"""
                            const int begin = 0;
                            var end = path.Length - 1;
                            const char quotationMark = '"';
                            if (path[begin] == quotationMark && path[end] == quotationMark)
                            {
                                path = path.Substring(begin + 1, path.Length - 2);
                            }

                            if (!File.Exists(path)) continue;
                            var entry = CreateEntry(path);
                            entry.ExecutableName = item;
                            entry.Source = this;
                            list.Add(entry);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Exception(e);
                    }
                }
            }
        }
    }
}