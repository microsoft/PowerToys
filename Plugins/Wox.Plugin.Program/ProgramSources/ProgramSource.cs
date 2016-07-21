using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Wox.Plugin.Program.ProgramSources
{
    [Serializable]
    public abstract class ProgramSource
    {
        public const char SuffixSeperator = ';';
        
        public int BonusPoints { get; set; } = 0;
        public bool Enabled { get; set; } = true;
        // happlebao todo: temp hack for program suffixes
        public string[] Suffixes { get; set; } = {"bat", "appref-ms", "exe", "lnk"};
        public int MaxDepth { get; set; } = -1;

        public abstract List<Program> LoadPrograms();

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
    }
}