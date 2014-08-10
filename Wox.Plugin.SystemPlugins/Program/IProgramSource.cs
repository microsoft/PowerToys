using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Wox.Plugin.SystemPlugins.Program
{
    public interface IProgramSource
    {
        List<Program> LoadPrograms();
        int BonusPoints { get; set; }
    }

    public abstract class AbstractProgramSource : IProgramSource
    {
        public abstract List<Program> LoadPrograms();

        public int BonusPoints
        {
            get; set;
        }

        protected Program CreateEntry(string file)
        {
            var p = new Program()
            {
                Title = Path.GetFileNameWithoutExtension(file),
                IcoPath = file,
                ExecutePath = file
            };

            switch (Path.GetExtension(file).ToLower())
            {
                case ".exe":
                    p.ExecuteName = global::System.IO.Path.GetFileName(file);
                    try
                    {
                        FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(file);
                        if (!string.IsNullOrEmpty(versionInfo.FileDescription))
                        {
                            p.Title = versionInfo.FileDescription;
                        }
                    }
                    catch (Exception) { }
                    break;
            }
            return p;
        }
    }
}
