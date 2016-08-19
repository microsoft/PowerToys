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
        public int MaxDepth { get; set; } = -1;
        internal string[] Suffixes { get; set; }  = { "" };

        public override List<Program> LoadPrograms()
        {
            if (Directory.Exists(Location) && MaxDepth >= -1)
            {
                var apps = new List<Program>();
                GetAppFromDirectory(apps, Location, 0);
                return apps;
            }
            else
            {
                return new List<Program>();
            }
        }

        private void GetAppFromDirectory(List<Program> apps, string path, int depth)
        {
            if (MaxDepth != depth)
            {
                foreach (var file in Directory.GetFiles(path))
                {
                    if (Suffixes.Any(o => file.EndsWith("." + o)))
                    {
                        Program p;
                        try
                        {
                            p = CreateEntry(file);
                        }
                        catch (Exception e)
                        {
                            var woxPluginException = new WoxPluginException("Program",
                                $"GetAppFromDirectory failed: {path}", e);
                            Log.Exception(woxPluginException);
                            continue;
                        }
                        p.Source = this;
                        apps.Add(p);
                    }
                }
                foreach (var d in Directory.GetDirectories(path))
                {
                    GetAppFromDirectory(apps, d, depth + 1);
                }
            }
        }
    }
}