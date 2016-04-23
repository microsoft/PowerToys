using System;
using System.Text.RegularExpressions;
using System.Threading;
using Wox.Infrastructure;

namespace Wox.Plugin.Program
{
    [Serializable]
    public class Program
    {
        private static readonly Regex AbbrRegexp = new Regex("[^A-Z0-9]", RegexOptions.Compiled);
        public string Title { get; set; }
        public string IcoPath { get; set; }
        public string ExecutePath { get; set; }
        public string ExecuteName { get; set; }
        public int Score { get; set; }
        public IProgramSource Source { get; set; }
    }
}