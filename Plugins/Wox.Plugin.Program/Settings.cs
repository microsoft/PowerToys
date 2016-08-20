using System.Collections.Generic;
using Wox.Plugin.Program.Programs;

namespace Wox.Plugin.Program
{
    public class Settings
    {
        public List<ProgramSource> ProgramSources { get; set; } = new List<ProgramSource>();
        public string[] ProgramSuffixes { get; set; } = {"bat", "appref-ms", "exe", "lnk"};

        public bool EnableStartMenuSource { get; set; } = true;

        public bool EnableRegistrySource { get; set; } = true;

        internal const char SuffixSeperator = ';';

        public class ProgramSource
        {
            public string Location { get; set; }
        }
    }
}
