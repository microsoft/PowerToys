using System;
using System.Collections.Generic;

namespace Wox.Plugin.Program
{
    [Serializable]
    public class ProgramSource
    {
        public string Location { get; set; }
        public string Type { get; set; }
        public int BonusPoints { get; set; }
        public bool Enabled { get; set; }
        public string[] Suffixes { get; set; }
        public const char SuffixSeperator = ';';
        public int MaxDepth { get; set; }
        public Dictionary<string, string> Meta { get; set; }

        public override string ToString()
        {
            return (Type ?? "") + ":" + Location ?? "";
        }
    }
}
