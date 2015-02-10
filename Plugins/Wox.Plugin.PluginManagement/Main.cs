using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace Wox.Plugin.PluginManagement
{
    public class Main : IPlugin,IPluginI18n
    {
        private static string APIBASE = "https://api.getwox.com";
        private static string PluginConfigName = "plugin.json";
        private static string pluginSearchUrl = APIBASE + "/plugin/search/";
        private PluginInitContext context;

        public List<Result> Query(Query query)
        {
            List<Result> results = new List<Result>();
            if (string.IsNullOrEmpty(query.Search))
            {
                results.Add(new Result("install <pluginName>", "Images\\plugin.png", "search and install wox plugins")
                {
                    Action = e => ChangeToInstallCommand()
                });
                results.Add(new Result("uninstall <pluginName>", "Images\\plugin.png", "uninstall plugin")
                {
                    Action = e => ChangeToUninstallCommand()
                });
                results.Add(new Result("list", "Images\\plugin.png", "list plugins installed")
                {
                    Action = e => ChangeToListCommand()
                });
                return results;
            }

            if (!string.IsNullOrEmpty(query.FirstSearch))
            {
                bool hit = false;
                switch (query.FirstSearch.ToLower())
                {
                    case "list":
                        hit = true;
                        results = ListInstalledPlugins();
                        break;

                    case "uninstall":
                        hit = true;
                        results = UnInstallPlugins(query);
                        break;

                    case "install":
                        hit = true;
                        if (!string.IsNullOrEmpty(query.SecondSearch))
                        {
                            results = InstallPlugin(query.SecondSearch);
                        }
                        break;
                }

                if (!hit)
                {
                    if ("install".Contains(query.FirstSearch.ToLower()))
                    {
                        results.Add(new Result("install <pluginName>", "Images\\plugin.png", "search and install wox plugins")
                        {
                            Action = e => ChangeToInstallCommand()
                        });
                    }
                    if ("uninstall".Contains(query.FirstSearch.ToLower()))
                    {
                        results.Add(new Result("uninstall <pluginName>", "Images\\plugin.png", "uninstall plugin")
                        {
                            Action = e => ChangeToUninstallCommand()
                        });
                    }
                    if ("list".Contains(query.FirstSearch.ToLower()))
                    {
                        results.Add(new Result("list", "Images\\plugin.png", "list plugins installed")
                        {
                            Action = e => ChangeToListCommand()
                        });
                    }
                }
            }

            return results;
        }

        private bool ChangeToListCommand()
        {
            if (context.CurrentPluginMetadata.ActionKeyword == "*")
            {
                context.API.ChangeQuery("list ");
            }
            else
            {
                context.API.ChangeQuery(string.Format("{0} list ", context.CurrentPluginMetadata.ActionKeyword));
            }
            return false;
        }

        private bool ChangeToUninstallCommand()
        {
            if (context.CurrentPluginMetadata.ActionKeyword == "*")
            {
                context.API.ChangeQuery("uninstall ");
            }
            else
            {
                context.API.ChangeQuery(string.Format("{0} uninstall ", context.CurrentPluginMetadata.ActionKeyword));
            }
            return false;
        }

        private bool ChangeToInstallCommand()
        {
            if (context.CurrentPluginMetadata.ActionKeyword == "*")
            {
                context.API.ChangeQuery("install ");
            }
            else
            {
                context.API.ChangeQuery(string.Format("{0} install ", context.CurrentPluginMetadata.ActionKeyword));
            }
            return false;
        }

        private List<Result> InstallPlugin(string queryPluginName)
        {
            List<Result> results = new List<Result>();
            HttpWebResponse response = HttpRequest.CreateGetHttpResponse(pluginSearchUrl + queryPluginName, context.Proxy);
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
                    context.API.ShowMsg("Coundn't parse api search results", "Please update your Wox!", string.Empty);
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

        private List<Result> UnInstallPlugins(Query query)
        {
            List<Result> results = new List<Result>();
            List<PluginMetadata> allInstalledPlugins = context.API.GetAllPlugins().Select(o => o.Metadata).ToList();
            if (!string.IsNullOrEmpty(query.SecondSearch))
            {
                allInstalledPlugins =
                    allInstalledPlugins.Where(o => o.Name.ToLower().Contains(query.SecondSearch.ToLower())).ToList();
            }

            foreach (PluginMetadata plugin in allInstalledPlugins)
            {
                var plugin1 = plugin;
                results.Add(new Result()
                {
                    Title = plugin.Name,
                    SubTitle = plugin.Description,
                    IcoPath = plugin.FullIcoPath,
                    Action = e =>
                    {
                        UnInstallPlugin(plugin1);
                        return false;
                    }
                });
            }
            return results;
        }

        private void UnInstallPlugin(PluginMetadata plugin)
        {
            string content = string.Format("Do you want to uninstall following plugin?\r\n\r\nName: {0}\r\nVersion: {1}\r\nAuthor: {2}", plugin.Name, plugin.Version, plugin.Author);
            if (MessageBox.Show(content, "Wox", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                File.Create(Path.Combine(plugin.PluginDirectory, "NeedDelete.txt")).Close();
                if (MessageBox.Show(
                    "You have uninstalled plugin " + plugin.Name + " successfully.\r\n Restart Wox to take effect?",
                    "Install plugin",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    ProcessStartInfo Info = new ProcessStartInfo();
                    Info.Arguments = "/C ping 127.0.0.1 -n 1 && \"" + Application.ExecutablePath + "\"";
                    Info.WindowStyle = ProcessWindowStyle.Hidden;
                    Info.CreateNoWindow = true;
                    Info.FileName = "cmd.exe";
                    Process.Start(Info);
                    context.API.CloseApp();
                }
            }
        }

        private List<Result> ListInstalledPlugins()
        {
            List<Result> results = new List<Result>();
            foreach (PluginMetadata plugin in context.API.GetAllPlugins().Select(o => o.Metadata))
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

        public void Init(PluginInitContext context)
        {
            this.context = context;
        }

        public string GetLanguagesFolder()
        {
            return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Languages");
        }

        public string GetTranslatedPluginTitle()
        {
            return context.API.GetTranslation("wox_plugin_plugin_management_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return context.API.GetTranslation("wox_plugin_plugin_management_plugin_description");
        }
    }
}
