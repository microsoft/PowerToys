using System;
using System.Collections.Generic;
using System.Text;

namespace Community.PowerToys.Run.Plugin.Everything
{
    internal class EverythingSettings
    {
            public List<ContextMenu> ContextMenus { get; } = new List<ContextMenu>();

            public int MaxSearchCount { get; set; } = 30;

            public bool UseLocationAsWorkingDir { get; set; }
    }
}
