using System.Collections.Generic;
using Wox.Plugin;

namespace Wox.Infrastructure.UserSettings
{
    public class PluginsSettings : BaseModel
    {
        public string PythonDirectory { get; set; }
        public Dictionary<string, Plugin> Plugins { get; set; } = new Dictionary<string, Plugin>();

        public void UpdatePluginSettings(List<PluginMetadata> metadatas)
        {
            foreach (var metadata in metadatas)
            {
                if (Plugins.ContainsKey(metadata.ID))
                {
                    var settings = Plugins[metadata.ID];
                    if (settings.ActionKeywords?.Count > 0)
                    {
                        metadata.ActionKeywords = settings.ActionKeywords;
                        metadata.ActionKeyword = settings.ActionKeywords[0];
                    }
                    metadata.Disabled = settings.Disabled;
                }
                else
                {
                    Plugins[metadata.ID] = new Plugin
                    {
                        ID = metadata.ID,
                        Name = metadata.Name,
                        ActionKeywords = metadata.ActionKeywords, 
                        Disabled = metadata.Disabled
                    };
                }
            }
        }
    }
    public class Plugin
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public List<string> ActionKeywords { get; set; } // a reference of the action keywords from plugin manager

        /// <summary>
        /// Used only to save the state of the plugin in settings
        /// </summary>
        public bool Disabled { get; set; }
    }
}
