using System;
using System.Collections.Generic;

namespace Wox.Infrastructure.Storage.UserSettings
{
    [Serializable]
    public class ProgramSource
    {
        public string Location { get; set; }
        public string Type { get; set; }
        public int BonusPoints { get; set; }
        public bool Enabled { get; set; }
        public Dictionary<string, string> Meta { get; set; }

        public override string ToString()
        {
            return (this.Type ?? "") + ":" + this.Location ?? "";
        }
    }
}
