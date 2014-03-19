using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Wox.Plugin.System
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
            Program p = new Program()
            {
                Title = global::System.IO.Path.GetFileNameWithoutExtension(file),
                IcoPath = file,
                ExecutePath = file
            };

            switch (global::System.IO.Path.GetExtension(file).ToLower())
            {
                case ".exe":
                    p.ExecuteName = global::System.IO.Path.GetFileName(file);
                    try
                    {
                        FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(file);
                        if (versionInfo.FileDescription != null && versionInfo.FileDescription != string.Empty) p.Title = versionInfo.FileDescription;
                    }
                    catch (Exception) { }
                    break;
            }
            return p;
        }
    }
}
