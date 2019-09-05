using System.Collections.Generic;
using System.IO;
using Wox.Plugin.Program.Programs;

namespace Wox.Plugin.Program
{
    public class Settings
    {
        public List<ProgramSource> ProgramSources { get; set; } = new List<ProgramSource>();
        public string[] ProgramSuffixes { get; set; } = {"bat", "appref-ms", "exe", "lnk"};

        public bool EnableStartMenuSource { get; set; } = true;

        public bool EnableRegistrySource { get; set; } = true;

        public bool EnableProgramSourceOnly { get; set; } = false;

        internal const char SuffixSeperator = ';';

        public class ProgramSource
        {
            private string name;

            public string Location { get; set; }
            public string Name { get => name ?? new DirectoryInfo(Location).Name; set => name = value; }
            public bool Enabled { get; set; } = true;
        }
    }
}
