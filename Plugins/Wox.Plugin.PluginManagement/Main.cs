using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace Wox.Plugin.PluginManagement
{
    public class WoxPluginResult
    {
        public string plugin_file;
        public string description;
        public int liked_count;
        public string name;
        public string version;
    }

    public class Main : IPlugin
    {
        private static string APIBASE = "https://api.getwox.com";
        private static string PluginPath = AppDomain.CurrentDomain.BaseDirectory + "Plugins";
        private static string PluginConfigName = "plugin.json";
        private static string pluginSearchUrl = APIBASE +"/plugin/search/";
        private PluginInitContext context;

        public List<Result> Query(Query query)
        {
            List<Result> results = new List<Result>();
            if (query.ActionParameters.Count == 0)
            {
                results.Add(new Result("wpm install <pluginName>", "Images\\plugin.png", "search and install wox plugins")
                {
                    Action = e =>
                    {
                        context.API.ChangeQuery("wpm install ");
                        return false;
                    }
                });
                results.Add(new Result("wpm uninstall <pluginName>", "Images\\plugin.png", "uninstall plugin")
                {
                    Action = e =>
                    {
                        context.API.ChangeQuery("wpm uninstall ");
                        return false;
                    }
                });
                results.Add(new Result("wpm list", "Images\\plugin.png", "list plugins installed")
                {
                    Action = e =>
                    {
                        context.API.ChangeQuery("wpm list");
                        return false;
                    }
                });
                return results;
            }

            if (query.ActionParameters.Count > 0)
            {
                bool hit = false;
                switch (query.ActionParameters[0].ToLower())
                {
                    case "list":
                        hit = true;
                        results = ListInstalledPlugins();
                        break;

                    case "uninstall":
                        hit = true;
                        results = ListUnInstalledPlugins(query);
                        break;

                    case "install":
                        hit = true;
                        if (query.ActionParameters.Count > 1)
                        {
                            results = InstallPlugin(query);
                        }
                        break;
                }

                if (!hit)
                {
                    if ("install".Contains(query.ActionParameters[0].ToLower()))
                    {
                        results.Add(new Result("wpm install <pluginName>", "Images\\plugin.png", "search and install wox plugins")
                        {
                            Action = e =>
                            {
                                context.API.ChangeQuery("wpm install ");
                                return false;
                            }
                        });
                    }
                    if ("uninstall".Contains(query.ActionParameters[0].ToLower()))
                    {
                        results.Add(new Result("wpm uninstall <pluginName>", "Images\\plugin.png", "uninstall plugin")
                        {
                            Action = e =>
                            {
                                context.API.ChangeQuery("wpm uninstall ");
                                return false;
                            }
                        });
                    }
                    if ("list".Contains(query.ActionParameters[0].ToLower()))
                    {
                        results.Add(new Result("wpm list", "Images\\plugin.png", "list plugins installed")
                        {
                            Action = e =>
                            {
                                context.API.ChangeQuery("wpm list");
                                return false;
                            }
                        });
                    }
                }
            }

            return results;
        }

        private List<Result> InstallPlugin(Query query)
        {
            List<Result> results = new List<Result>();
            HttpWebResponse response = HttpRequest.CreateGetHttpResponse(pluginSearchUrl + query.ActionParameters[1], context.Proxy);
            Stream s = response.GetResponseStream();
            if (s != null)
            {
                StreamReader reader = new StreamReader(s, Encoding.UTF8);
                string json = reader.ReadToEnd();
                List<WoxPluginResult> searchedPlugins = null;
                try
                {
                    searchedPlugins = JsonConvert.DeserializeObject<List<WoxPluginResult>>(json);
                }
                catch
                {
                    context.API.ShowMsg("Coundn't parse api search results", "Please update your Wox!",string.Empty);
                    return results;
                }

                foreach (WoxPluginResult r in searchedPlugins)
                {
                    WoxPluginResult r1 = r;
                    results.Add(new Result()
                    {
                        Title = r.name,
                        SubTitle = r.description,
                        IcoPath = "Images\\plugin.png",
                        Action = e =>
                        {
                            DialogResult result = MessageBox.Show("Are your sure to install " + r.name + " plugin",
                                "Install plugin", MessageBoxButtons.YesNo);

                            if (result == DialogResult.Yes)
                            {
                                string folder = Path.Combine(Path.GetTempPath(), "WoxPluginDownload");
                                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                                string filePath = Path.Combine(folder, Guid.NewGuid().ToString() + ".wox");

                                context.API.StartLoadingBar();
                                ThreadPool.QueueUserWorkItem(delegate
                                {
                                    using (WebClient Client = new WebClient())
                                    {
                                        try
                                        {
                                            string pluginUrl = APIBASE + "/media/" + r1.plugin_file;
                                            Client.DownloadFile(pluginUrl, filePath);
                                            context.API.InstallPlugin(filePath);
                                            context.API.ReloadPlugins();
                                        }
                                        catch (Exception exception)
                                        {
                                            MessageBox.Show("download plugin " + r.name + "failed. " + exception.Message);
                                        }
                                        finally
                                        {
                                            context.API.StopLoadingBar();
                                        }
                                    }
                                });
                            }
                            return false;
                        }
                    });
                }
            }
            return results;
        }

        private List<Result> ListUnInstalledPlugins(Query query)
        {
            List<Result> results = new List<Result>();
            List<PluginMetadata> allInstalledPlugins = ParseUserPlugins();
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
                    IcoPath = plugin.FullIcoPath,
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
            string content = string.Format("Do you want to uninstall following plugin?\r\n\r\nName: {0}\r\nVersion: {1}\r\nAuthor: {2}", plugin.Name, plugin.Version, plugin.Author);
            if (MessageBox.Show(content, "Wox", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                File.Create(Path.Combine(plugin.PluginDirectory, "NeedDelete.txt")).Close();
                MessageBox.Show("This plugin has been removed, restart Wox to take effect");
            }
        }

        private List<Result> ListInstalledPlugins()
        {
            List<Result> results = new List<Result>();
            foreach (PluginMetadata plugin in ParseUserPlugins())
            {
                results.Add(new Result()
                {
                    Title = plugin.Name + " - " + plugin.ActionKeyword,
                    SubTitle = plugin.Description,
                    IcoPath = plugin.FullIcoPath
                });
            }
            return results;
        }

        private static List<PluginMetadata> ParseUserPlugins()
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
                metadata.PluginType = PluginType.User;
                metadata.PluginDirectory = pluginDirectory;
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
