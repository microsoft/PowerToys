using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Wox.Plugin
{
    public class PluginMetadata
    {
        private int configVersion = 1;

        /// <summary>
        /// if we need to change the plugin config in the futher, use this to
        /// indicate config version
        /// </summary>
        public int ConfigVersion
        {
            get { return configVersion; }
            set { configVersion = value; }
        }
        public string ID { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public string Version { get; set; }
        public string Language { get; set; }
        public string Description { get; set; }

        public string ExecuteFilePath
        {
            get { return Path.Combine(PluginDirecotry, ExecuteFileName); }
        }

        public string ExecuteFileName { get; set; }
        public string PluginDirecotry { get; set; }
        public string ActionKeyword { get; set; }
        public PluginType PluginType { get; set; }
    }
}
