using System;
using System.Text.RegularExpressions;
using System.Threading;
using Wox.Infrastructure;
using Wox.Plugin.Program.ProgramSources;

namespace Wox.Plugin.Program
{
    [Serializable]
    public class Program
    {
        public string Title { get; set; }
        public string IcoPath { get; set; }
        public string Path { get; set; }
        public string Directory { get; set; }
        public string ExecutableName { get; set; }
        public int Score { get; set; }
        public ProgramSource Source { get; set; }
    }
}