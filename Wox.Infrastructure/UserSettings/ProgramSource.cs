using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Infrastructure.UserSettings
{
    [Serializable]
    public class ProgramSource
    {
        public string Location { get; set; }
        public string Type { get; set; }
        public int BounsPoints { get; set; }
        public bool Enabled { get; set; }
        public Dictionary<string, string> Meta { get; set; }
    }
}
