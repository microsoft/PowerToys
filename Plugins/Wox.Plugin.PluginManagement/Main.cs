using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using Newtonsoft.Json;

namespace Wox.Plugin.PluginManagement
{
    public class Main : IPlugin, IPluginI18n
    {
        private static string APIBASE = "https://api.getwox.com";
        private static string PluginConfigName = "plugin.json";
        private static string pluginSearchUrl = APIBASE + "/plugin/search/";
        private const string ListCommand = "list";
        private const string InstallCommand = "install";
        private const string UninstallCommand = "uninstall";
        private PluginInitContext context;

        public List<Result> Query(Query query)
        {
            List<Result> results = new List<Result>();

            if (string.IsNullOrEmpty(query.Search))
            {
                results.Add(ResultForListCommandAutoComplete(query));
                results.Add(ResultForInstallCommandAutoComplete(query));
                results.Add(ResultForUninstallCommandAutoComplete(query));
                return results;
            }

            string command = query.FirstSearch.ToLower();
            if (string.IsNullOrEmpty(command)) return results;

            if (command == ListCommand)
            {
                return ResultForListInstalledPlugins();
            }
            if (command == UninstallCommand)
            {
                return ResultForUnInstallPlugin(query);
            }
            if (command == InstallCommand)
            {
                return ResultForInstallPlugin(query);
            }

            if (InstallCommand.Contains(command))
            {
                results.Add(ResultForInstallCommandAutoComplete(query));
            }
            if (UninstallCommand.Contains(command))
            {
                results.Add(ResultForUninstallCommandAutoComplete(query));
            }
            if (ListCommand.Contains(command))
            {
                results.Add(ResultForListCommandAutoComplete(query));
            }

            return results;
        }

        private Result ResultForListCommandAutoComplete(Query query)
        {
            string title = ListCommand;
            string subtitle = "list installed plugins";
            return ResultForCommand(query, ListCommand, title, subtitle);
        }

        private Result ResultForInstallCommandAutoComplete(Query query)
        {
            string title = $"{InstallCommand} <Package Name>";
            string subtitle = "list installed plugins";
            return ResultForCommand(query, InstallCommand, title, subtitle);
        }

        private Result ResultForUninstallCommandAutoComplete(Query query)
        {
            string title = $"{UninstallCommand} <Package Name>";
            string subtitle = "list installed plugins";
            return ResultForCommand(query, UninstallCommand, title, subtitle);
        }

        private Result ResultForCommand(Query query, string command, string title, string subtitle)
        {
            const string seperater = Plugin.Query.TermSeperater;
            var result = new Result
            {
                Title = title,
                IcoPath = "Images\\plugin.png",
                SubTitle = subtitle,
                Action = e =>
                {
                    context.API.ChangeQuery($"{query.ActionKeyword}{seperater}{command}{seperater}");
                    return false;
                }
            };
            return result;
        }

        private List<Result> ResultForInstallPlugin(Query query)
        {
            List<Result> results = new List<Result>();
            string pluginName = query.SecondSearch;
            if (string.IsNullOrEmpty(pluginName)) return results;
            HttpWebResponse response = HttpRequest.CreateGetHttpResponse(pluginSearchUrl + pluginName, context.Proxy);
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
                    results.Add(new Result
                    {
                        Title = r.name,
                        SubTitle = r.description,
                        IcoPath = "Images\\plugin.png",
                        Action = e =>
                        {
                            MessageBoxResult result = MessageBox.Show("Are your sure to install " + r.name + " plugin",
                                "Install plugin", MessageBoxButton.YesNo);

                            if (result == MessageBoxResult.Yes)
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

        private List<Result> ResultForUnInstallPlugin(Query query)
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
                results.Add(new Result
                {
                    Title = plugin.Name,
                    SubTitle = plugin.Description,
                    IcoPath = plugin.FullIcoPath,
                    Action = e =>
                    {
                        UnInstallPlugin(plugin);
                        return false;
                    }
                });
            }
            return results;
        }

        private void UnInstallPlugin(PluginMetadata plugin)
        {
            string content = string.Format("Do you want to uninstall following plugin?\r\n\r\nName: {0}\r\nVersion: {1}\r\nAuthor: {2}", plugin.Name, plugin.Version, plugin.Author);
            if (MessageBox.Show(content, "Wox", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                File.Create(Path.Combine(plugin.PluginDirectory, "NeedDelete.txt")).Close();
                if (MessageBox.Show(
                    "You have uninstalled plugin " + plugin.Name + " successfully.\r\n Restart Wox to take effect?",
                    "Install plugin",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    ProcessStartInfo Info = new ProcessStartInfo();
                    Info.Arguments = "/C ping 127.0.0.1 -n 1 && \"" + Assembly.GetExecutingAssembly().Location + "\"";
                    Info.WindowStyle = ProcessWindowStyle.Hidden;
                    Info.CreateNoWindow = true;
                    Info.FileName = "cmd.exe";
                    Process.Start(Info);
                    context.API.CloseApp();
                }
            }
        }

        private List<Result> ResultForListInstalledPlugins()
        {
            List<Result> results = new List<Result>();
            foreach (PluginMetadata plugin in context.API.GetAllPlugins().Select(o => o.Metadata))
            {
                string actionKeywordString = string.Join(" or ", plugin.ActionKeywords.ToArray());
                results.Add(new Result
                {
                    Title = $"{plugin.Name} - Action Keywords: {actionKeywordString}",
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
