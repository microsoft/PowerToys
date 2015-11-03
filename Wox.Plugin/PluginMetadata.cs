using System;
using System.IO;

namespace Wox.Plugin
{
    public class PluginMetadata
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public string Version { get; set; }
        public string Language { get; set; }
        public string Description { get; set; }

        public string Website { get; set; }

        public string ExecuteFilePath
        {
            get { return Path.Combine(PluginDirectory, ExecuteFileName); }
        }

        public string ExecuteFileName { get; set; }

        public string PluginDirectory { get; set; }

        public string ActionKeyword { get; set; }

        public string IcoPath { get; set; }

        public override string ToString()
        {
            return Name;
        }

        public string FullIcoPath
        {
            get
            {
                // Return the default icon if IcoPath is empty
                if (string.IsNullOrEmpty(IcoPath))
                    return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images\\work.png");

                if (IcoPath.StartsWith("data:"))
                {
                    return IcoPath;
                }

                return Path.Combine(PluginDirectory, IcoPath);
            }
        }
    }
}
