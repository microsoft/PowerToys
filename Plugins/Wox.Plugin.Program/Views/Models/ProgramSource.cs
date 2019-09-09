using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wox.Plugin.Program.Views.Models
{
    public class ProgramSource
    {
        private string name;

        public string Location { get; set; }
        public string Name { get => name ?? new DirectoryInfo(Location).Name; set => name = value; }
        public string UniqueIdentifier { get; set; }
        public bool Enabled { get; set; } = true;
    }
}
