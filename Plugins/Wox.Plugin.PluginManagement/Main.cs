using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using Wox.Infrastructure.Http;
using Wox.Infrastructure.Logger;

namespace Wox.Plugin.PluginManagement
{
    public class Main : IPlugin, IPluginI18n
    {
        private static string APIBASE = "http://api.wox.one";
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
            string json;
            try
            {
                json = Http.Get(pluginSearchUrl + pluginName).Result;
            }
            catch (WebException e)
            {
                //todo happlebao add option in log to decide give user prompt or not
                context.API.ShowMsg("PluginManagement.ResultForInstallPlugin: Can't connect to Wox plugin website, check your conenction");
                Log.Exception("|PluginManagement.ResultForInstallPlugin|Can't connect to Wox plugin website, check your conenction", e);
                return new List<Result>();
            }
            List<WoxPluginResult> searchedPlugins;
            try
            {
                searchedPlugins = JsonConvert.DeserializeObject<List<WoxPluginResult>>(json);
            }
            catch (JsonSerializationException e)
            {
                context.API.ShowMsg("PluginManagement.ResultForInstallPlugin: Coundn't parse api search results, Please update your Wox!");
                Log.Exception("|PluginManagement.ResultForInstallPlugin|Coundn't parse api search results, Please update your Wox!", e);
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
                    Action = c =>
                    {
                        MessageBoxResult result = MessageBox.Show("Are you sure you wish to install the \'" + r.name + "\' plugin",
                            "Install plugin", MessageBoxButton.YesNo);

                        if (result == MessageBoxResult.Yes)
                        {
                            string folder = Path.Combine(Path.GetTempPath(), "WoxPluginDownload");
                            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                            string filePath = Path.Combine(folder, Guid.NewGuid().ToString() + ".wox");

                            string pluginUrl = APIBASE + "/media/" + r1.plugin_file;

                            try
                            {
                                Http.Download(pluginUrl, filePath);
                            }
                            catch (WebException e)
                            {
                                context.API.ShowMsg($"PluginManagement.ResultForInstallPlugin: download failed for <{r.name}>");
                                Log.Exception($"|PluginManagement.ResultForInstallPlugin|download failed for <{r.name}>", e);
                                return false;
                            }
                            context.API.InstallPlugin(filePath);
                        }
                        return false;
                    }
                });
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
                    IcoPath = plugin.IcoPath,
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
            string content = $"Do you want to uninstall following plugin?{Environment.NewLine}{Environment.NewLine}" +
                             $"Name: {plugin.Name}{Environment.NewLine}" +
                             $"Version: {plugin.Version}{Environment.NewLine}" +
                             $"Author: {plugin.Author}";
            if (MessageBox.Show(content, "Wox", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                File.Create(Path.Combine(plugin.PluginDirectory, "NeedDelete.txt")).Close();
                var result = MessageBox.Show($"You have uninstalled plugin {plugin.Name} successfully.{Environment.NewLine}" +
                                             "Restart Wox to take effect?",
                                             "Install plugin", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    context.API.RestarApp();
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
                    IcoPath = plugin.IcoPath
                });
            }
            return results;
        }

        public void Init(PluginInitContext context)
        {
            this.context = context;
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
