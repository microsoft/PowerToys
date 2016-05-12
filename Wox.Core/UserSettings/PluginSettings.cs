using System.Collections.Generic;
using System.Linq;
using Wox.Core.Plugin;
using Wox.Plugin;

namespace Wox.Core.UserSettings
{
    public class PluginsSettings
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
                }
                else
                {
                    Plugins[metadata.ID] = new Plugin
                    {
                        ID = metadata.ID,
                        Name = metadata.Name,
                        ActionKeywords = metadata.ActionKeywords,
                        Disabled = false
                    };
                }
            }
        }

        public void UpdateActionKeyword(PluginMetadata metadata)
        {
            var settings = Plugins[metadata.ID];
            settings.ActionKeywords = metadata.ActionKeywords;
        }
    }
    public class Plugin
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public List<string> ActionKeywords { get; set; }
        public bool Disabled { get; set; }
    }
}
