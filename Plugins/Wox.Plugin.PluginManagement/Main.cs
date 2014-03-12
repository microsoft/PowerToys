using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace Wox.Plugin.PluginManagement
{
    public class Main : IPlugin
    {
        private static string PluginPath = AppDomain.CurrentDomain.BaseDirectory + "Plugins";
        private static string PluginConfigName = "plugin.json";
        private PluginInitContext context;

        public List<Result> Query(Query query)
        {
            List<Result> results = new List<Result>();
            if (query.ActionParameters.Count == 0)
            {
                results.Add(new Result()
                {
                    Title = "wpm list",
                    SubTitle = "list plugins installed",
                    IcoPath = "Images\\plugin.png",
                    Action = e =>
                    {
                        context.ChangeQuery("wpm list");
                        return false;
                    }
                });
                results.Add(new Result()
                {
                    Title = "wpm uninstall <pluginName>",
                    SubTitle = "uninstall plugin",
                    IcoPath = "Images\\plugin.png",
                    Action = e =>
                    {
                        context.ChangeQuery("wpm uninstall ");
                        return false;
                    }
                });
                return results;
            }

            if (query.ActionParameters.Count > 0)
            {
                switch (query.ActionParameters[0].ToLower())
                {
                    case "list":
                        results = ListInstalledPlugins();
                        break;

                    case "uninstall":
                        results = ListUnInstalledPlugins(query);
                        break;
                }
            }

            return results;
        }

        private List<Result> ListUnInstalledPlugins(Query query)
        {
            List<Result> results = new List<Result>();
            List<PluginMetadata> allInstalledPlugins = ParseThirdPartyPlugins();
            if (query.ActionParameters.Count > 1)
            {
                string pluginName = query.ActionParameters[1];
                allInstalledPlugins =
                    allInstalledPlugins.Where(o => o.Name.ToLower().Contains(pluginName.ToLower())).ToList();
            }

            foreach (PluginMetadata plugin in allInstalledPlugins)
            {
                results.Add(new Result()
                {
                    Title = plugin.Name,
                    SubTitle = plugin.Description,
                    IcoPath = "Images\\plugin.png",
                    Action = e =>
                    {
                        UnInstalledPlugins(plugin);
                        return false;
                    }
                });
            }
            return results;
        }

        private void UnInstalledPlugins(PluginMetadata plugin)
        {
            string content = string.Format("Do you want to uninstall following plugin?\r\n\r\nName: {0}\r\nVersion: {1}\r\nAuthor: {2}",plugin.Name, plugin.Version, plugin.Author);
            if (MessageBox.Show(content, "Wox", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                File.Create(Path.Combine(plugin.PluginDirecotry, "NeedDelete.txt"));
                MessageBox.Show("This plugin has been removed, restart Wox to take effect");
            }
        }

        private List<Result> ListInstalledPlugins()
        {
            List<Result> results = new List<Result>();
            foreach (PluginMetadata plugin in ParseThirdPartyPlugins())
            {
                results.Add(new Result()
                {
                    Title = plugin.Name,
                    SubTitle = plugin.Description,
                    IcoPath = "Images\\plugin.png"
                });
            }
            return results;
        }

        private static List<PluginMetadata> ParseThirdPartyPlugins()
        {
            List<PluginMetadata> pluginMetadatas = new List<PluginMetadata>();
            if (!Directory.Exists(PluginPath))
                Directory.CreateDirectory(PluginPath);

            string[] directories = Directory.GetDirectories(PluginPath);
            foreach (string directory in directories)
            {
                PluginMetadata metadata = GetMetadataFromJson(directory);
                if (metadata != null) pluginMetadatas.Add(metadata);
            }

            return pluginMetadatas;
        }

        private static PluginMetadata GetMetadataFromJson(string pluginDirectory)
        {
            string configPath = Path.Combine(pluginDirectory, PluginConfigName);
            PluginMetadata metadata;

            if (!File.Exists(configPath))
            {
                return null;
            }

            try
            {
                metadata = JsonConvert.DeserializeObject<PluginMetadata>(File.ReadAllText(configPath));
                metadata.PluginType = PluginType.ThirdParty;
                metadata.PluginDirecotry = pluginDirectory;
            }
            catch (Exception)
            {
                string error = string.Format("Parse plugin config {0} failed: json format is not valid", configPath);
                return null;
            }


            if (!AllowedLanguage.IsAllowed(metadata.Language))
            {
                string error = string.Format("Parse plugin config {0} failed: invalid language {1}", configPath,
                    metadata.Language);
                return null;
            }
            if (!File.Exists(metadata.ExecuteFilePath))
            {
                string error = string.Format("Parse plugin config {0} failed: ExecuteFile {1} didn't exist", configPath,
                    metadata.ExecuteFilePath);
                return null;
            }

            return metadata;
        }

        public void Init(PluginInitContext context)
        {
            this.context = context;
        }
    }
}
