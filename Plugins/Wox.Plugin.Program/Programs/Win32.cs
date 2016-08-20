using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Wox.Infrastructure.Exception;
using Wox.Infrastructure.Logger;

namespace Wox.Plugin.Program.Programs
{
    [Serializable]
    public class Win32
    {
        public string Title { get; set; }
        public string IcoPath { get; set; }
        public string ExecutablePath { get; set; }
        public string Directory { get; set; }
        public string ExecutableName { get; set; }
        public int Score { get; set; }

        protected static Win32 CreateEntry(string file)
        {
            var p = new Win32
            {
                Title = Path.GetFileNameWithoutExtension(file),
                IcoPath = file,
                ExecutablePath = file,
                Directory = System.IO.Directory.GetParent(file).FullName
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

        protected static void GetAppFromDirectory(List<Win32> apps, string directory, int depth, string[] suffixes)
        {
            if (depth == -1)
            {
            }
            else if (depth > 0)
            {
                depth = depth - 1;
            }
            else
            {
                return;
            }

            foreach (var f in System.IO.Directory.GetFiles(directory))
            {
                if (suffixes.Any(o => f.EndsWith("." + o)))
                {
                    Win32 p;
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
            foreach (var d in System.IO.Directory.GetDirectories(directory))
            {
                GetAppFromDirectory(apps, d, depth, suffixes);
            }
        }
    }
}
