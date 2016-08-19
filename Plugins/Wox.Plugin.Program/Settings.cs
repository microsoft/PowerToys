using System;
using System.Collections.Generic;
using Wox.Plugin.Program.ProgramSources;

namespace Wox.Plugin.Program
{
    public class Settings
    {
        public List<FileSystemProgramSource> ProgramSources { get; set; } = new List<FileSystemProgramSource>();
        public string[] ProgramSuffixes { get; set; } = {"bat", "appref-ms", "exe", "lnk"};

        public bool EnableStartMenuSource { get; set; } = true;

        public bool EnableRegistrySource { get; set; } = true;

        internal const char SuffixSeperator = ';';
    }
}
