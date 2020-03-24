using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wox.Plugin.Indexer
{
    public class Settings
    {
        public List<ContextMenu> ContextMenus = new List<ContextMenu>();
        public int MaxSearchCount { get; set; } = 100;
        public bool UseLocationAsWorkingDir { get; set; } = false;
    }

    public class ContextMenu
    {
        public string Name { get; set; }
        public string Command { get; set; }
        public string Argument { get; set; }
        public string ImagePath { get; set; }
    }
}
