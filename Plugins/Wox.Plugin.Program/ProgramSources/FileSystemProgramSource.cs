using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wox.Infrastructure.Exception;
using Wox.Infrastructure.Logger;

namespace Wox.Plugin.Program.ProgramSources
{
    [Serializable]
    public class FileSystemProgramSource : ProgramSource
    {
        public string Location { get; set; } = "";

        public override List<Program> LoadPrograms()
        {
            var list = new List<Program>();
            if (Directory.Exists(Location))
            {
                GetAppFromDirectory(Location, list);
                FileChangeWatcher.AddWatch(Location, Suffixes);
            }
            return list;
        }

        private void GetAppFromDirectory(string path, List<Program> list)
        {
            GetAppFromDirectory(path, list, 0);
        }

        private void GetAppFromDirectory(string path, List<Program> list, int depth)
        {
            if (MaxDepth != -1 && depth > MaxDepth)
            {
                return;
            }
            try
            {
                foreach (var file in Directory.GetFiles(path))
                {
                    if (Suffixes.Any(o => file.EndsWith("." + o)))
                    {
                        var p = CreateEntry(file);
                        p.Source = this;
                        list.Add(p);
                    }
                }

                foreach (var subDirectory in Directory.GetDirectories(path))
                {
                    GetAppFromDirectory(subDirectory, list, depth + 1);
                }
            }
            catch (Exception e)
            {
                var woxPluginException = new WoxPluginException("Program", $"GetAppFromDirectory failed: {path}", e);
                Log.Exception(woxPluginException);
            }
        }
        public override string ToString()
        {
            var display = GetType().Name + Location;
            return display;
        }
    }
}