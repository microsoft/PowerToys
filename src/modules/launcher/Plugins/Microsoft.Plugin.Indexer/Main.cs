using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Wox.Plugin;
using System.IO;
using System.ComponentModel;
using Wox.Infrastructure.Storage;
using Microsoft.Plugin.Indexer.SearchHelper;
using Microsoft.Search.Interop;
using Microsoft.PowerToys.Settings.UI.Lib;
using System.Windows.Controls;
using Wox.Infrastructure.Logger;
using System.Text.RegularExpressions;
using Microsoft.Plugin.Indexer.DriveDetection;

namespace Microsoft.Plugin.Indexer
{
    class Main : ISettingProvider, IPlugin, ISavable, IPluginI18n, IContextMenu
    {

        // This variable contains metadata about the Plugin
        private PluginInitContext _context;

        // This variable contains information about the context menus
        private Settings _settings;

        // Contains information about the plugin stored in json format
        private PluginJsonStorage<Settings> _storage;

        // To access Windows Search functionalities
        private readonly WindowsSearchAPI _api = new WindowsSearchAPI(new OleDBSearch());

        // To obtain information regarding the drives that are indexed
        private readonly IndexerDriveDetection _driveDetection = new IndexerDriveDetection(new RegistryWrapper());

        // Reserved keywords in oleDB
        private string ReservedStringPattern = @"^[\/\\\$\%]+$";
        private string WarningIconPath { get; set; }
        private IContextMenu _contextMenuLoader;

        // To save the configurations of plugins
        public void Save()
        {
            _storage.Save();
        }

        // This function uses the Windows indexer and returns the list of results obtained
        public List<Result> Query(Query query)
        {
            var results = new List<Result>();

            if (_driveDetection.DisplayResults())
            {
                if (!string.IsNullOrEmpty(query.Search))
                {
                    var searchQuery = query.Search;
                    if (_settings.MaxSearchCount <= 0)
                    {
                        _settings.MaxSearchCount = 50;
                    }

                    var regexMatch = Regex.Match(searchQuery, ReservedStringPattern);

                    if (!regexMatch.Success)
                    {
                        try
                        {
                            var searchResultsList = _api.Search(searchQuery, maxCount: _settings.MaxSearchCount).ToList();
                            foreach (var searchResult in searchResultsList)
                            {
                                var path = searchResult.Path;
                                var toolTipTitle = string.Format("{0} : {1}", _context.API.GetTranslation("Microsoft_plugin_indexer_name"), searchResult.Title);
                                var toolTipText = string.Format("{0} : {1}", _context.API.GetTranslation("Microsoft_plugin_indexer_path"), path);
                                string workingDir = null;
                                if (_settings.UseLocationAsWorkingDir)
                                    workingDir = Path.GetDirectoryName(path);

                                Result r = new Result();
                                r.Title = searchResult.Title;
                                r.SubTitle = "Search: " + path;
                                r.IcoPath = path;
                                r.ToolTipData = new ToolTipData(toolTipTitle, toolTipText);
                                r.Action = c =>
                                {
                                    bool hide;
                                    try
                                    {
                                        Process.Start(new ProcessStartInfo
                                        {
                                            FileName = path,
                                            UseShellExecute = true,
                                            WorkingDirectory = workingDir
                                        });
                                        hide = true;
                                    }
                                    catch (Win32Exception)
                                    {
                                        var name = $"Plugin: {_context.CurrentPluginMetadata.Name}";
                                        var msg = "Can't Open this file";
                                        _context.API.ShowMsg(name, msg, string.Empty);
                                        hide = false;
                                    }
                                    return hide;
                                };
                                r.ContextData = searchResult;

                                //If the result is a directory, then it's display should show a directory.
                                if (Directory.Exists(path))
                                {
                                    r.QueryTextDisplay = path;
                                }

                                results.Add(r);
                            }
                        }
                        catch (InvalidOperationException)
                        {
                            //The connection has closed, internal error of ExecuteReader()
                            //Not showing this exception to the users
                        }
                        catch (Exception ex)
                        {
                            Log.Info(ex.ToString());
                        }
                    }
                }
            }
            else
            {
                results.Add(new Result
                {
                    Title = _context.API.GetTranslation("Microsoft_plugin_indexer_drivedetectionwarning"),
                    SubTitle = _context.API.GetTranslation("Microsoft_plugin_indexer_disable_warning_in_settings"),
                    IcoPath = WarningIconPath,
                    Action = e =>
                    {
                        try
                        {
                            Process.Start(GetWindowsSearchSettingsProcessInfo());
                        }
                        catch (Exception ex)
                        {
                            Log.Exception("Microsoft.Plugin.Indexer", $"Unable to launch Windows Search Settings: {ex.Message}", ex, "Query");
                        }
                        return true;
                    }
                });
            }

            return results;
        }

        public void Init(PluginInitContext context)
        {
            // initialize the context of the plugin
            _context = context;
            _contextMenuLoader = new ContextMenuLoader(context);
            _storage = new PluginJsonStorage<Settings>();
            _settings = _storage.Load();
            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(_context.API.GetCurrentTheme());
        }

        // Todo : Update with theme based IconPath
        private void UpdateIconPath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                WarningIconPath = "Images/Warning.light.png";
            }
            else
            {
                WarningIconPath = "Images/Warning.dark.png";
            }
        }

        private void OnThemeChanged(Theme _, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        // TODO: Localize the strings
        // Set the Plugin Title
        public string GetTranslatedPluginTitle()
        {
            return "Windows Indexer Plugin";
        }

        // TODO: Localize the string
        // Set the plugin Description
        public string GetTranslatedPluginDescription()
        {
            return "Returns files and folders";
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            return _contextMenuLoader.LoadContextMenus(selectedResult);
        }
        public void UpdateSettings(PowerLauncherSettings settings)
        {
            _settings.MaxSearchCount = settings.Properties.MaximumNumberOfResults;
            _driveDetection.IsDriveDetectionWarningCheckBoxSelected = settings.Properties.DisableDriveDetectionWarning;
        }
        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        // Returns the Process Start Information for the new Windows Search Settings
        public ProcessStartInfo GetWindowsSearchSettingsProcessInfo()
        {
            var ps = new ProcessStartInfo("ms-settings:cortana-windowssearch")
            {
                UseShellExecute = true,
                Verb = "open"
            };

            return ps;
        }

    }
}
