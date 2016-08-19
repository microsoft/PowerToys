using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Wox.Infrastructure.Exception;
using Wox.Infrastructure.Logger;

namespace Wox.Plugin.Program.ProgramSources
{
    [Serializable]
    public abstract class Win32 : ProgramSource
    {
        public int MaxDepth { get; set; } = -1;
        public string[] Suffixes { get; set; } = { "" };

        protected Program CreateEntry(string file)
        {
            var p = new Program
            {
                Title = Path.GetFileNameWithoutExtension(file),
                IcoPath = file,
                Path = file,
                Directory = Directory.GetParent(file).FullName
            };

            switch (Path.GetExtension(file).ToLower())
            {
                case ".exe":
                    p.ExecutableName = Path.GetFileName(file);
                    try
                    {
                        var versionInfo = FileVersionInfo.GetVersionInfo(file);
                        if (!string.IsNullOrEmpty(versionInfo.FileDescription))
                        {
                            p.Title = versionInfo.FileDescription;
                        }
                    }
                    catch (Exception)
                    {
                    }
                    break;
            }
            return p;
        }

        protected void GetAppFromDirectory(List<Program> apps, string directory, int depth)
        {
            if (MaxDepth == -1 || MaxDepth < depth)
            {
                foreach (var f in Directory.GetFiles(directory))
                {
                    if (Suffixes.Any(o => f.EndsWith("." + o)))
                    {
                        Program p;
                        try
                        {
                            p = CreateEntry(f);
                        }
                        catch (Exception e)
                        {
                            var woxPluginException = new WoxPluginException("Program",
                                $"GetAppFromDirectory failed: {directory}", e);
                            Log.Exception(woxPluginException);
                            continue;
                        }
                        apps.Add(p);
                    }
                }
                foreach (var d in Directory.GetDirectories(directory))
                {
                    GetAppFromDirectory(apps, d, depth - 1);
                }
            }
        }
    }
}
