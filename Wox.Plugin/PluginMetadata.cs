using System;
using System.Collections.Generic;
using System.IO;

namespace Wox.Plugin
{
    public class PluginMetadata
    {
        private string _pluginDirectory;
        public string ID { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public string Version { get; set; }
        public string Language { get; set; }
        public string Description { get; set; }

        public string Website { get; set; }

        public string ExecuteFilePath { get; private set;}

        public string ExecuteFileName { get; set; }

        public string PluginDirectory
        {
            get { return _pluginDirectory; }
            internal set
            {
                _pluginDirectory = value;
                ExecuteFilePath = Path.Combine(value, ExecuteFileName);
                IcoPath = Path.Combine(value, IcoPath);
            }
        }

        [Obsolete("Use ActionKeywords instead, because Wox now support multiple action keywords. This will be remove in v1.3.0")]
        public string ActionKeyword { get; set; }

        public List<string> ActionKeywords { get; set; }

        public string IcoPath { get; set;}

        public override string ToString()
        {
            return Name;
        }

        [Obsolete("Use IcoPath")]
        public string FullIcoPath => IcoPath;
    }
}
