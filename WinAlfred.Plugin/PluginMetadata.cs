using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WinAlfred.Plugin
{
    public class PluginMetadata
    {
        public string Name { get; set; }
        public string Author { get; set; }
        public string Version { get; set; }
        public string Language { get; set; }
        public string Description { get; set; }
        public string ExecuteFilePath { get; set; }
        public string ExecuteFileName { get; set; }
        public string PluginDirecotry { get; set; }
        public string ActionKeyword { get; set; }
    }
}
