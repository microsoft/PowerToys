// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Abstractions;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using ManagedCommon;
using Microsoft.Plugin.Indexer.DriveDetection;
using Microsoft.Plugin.Indexer.SearchHelper;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.Search.Interop;
using Wox.Infrastructure.Storage;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Microsoft.Plugin.Indexer
{
    internal class Main : ISettingProvider, IPlugin, ISavable, IPluginI18n, IContextMenu, IDisposable, IDelayedExecutionPlugin
    {
        private const string DisableDriveDetectionWarning = nameof(DisableDriveDetectionWarning);
        private static readonly IFileSystem _fileSystem = new FileSystem();

        // This variable contains metadata about the Plugin
        private PluginInitContext _context;

        // This variable contains information about the context menus
        private IndexerSettings _settings;

        // Contains information about the plugin stored in json format
        private PluginJsonStorage<IndexerSettings> _storage;

        // To access Windows Search functionalities
        private static readonly OleDBSearch _search = new OleDBSearch();
        private readonly WindowsSearchAPI _api = new WindowsSearchAPI(_search);

        // To obtain information regarding the drives that are indexed
        private readonly IndexerDriveDetection _driveDetection = new IndexerDriveDetection(new RegistryWrapper(), new DriveDetection.DriveInfoWrapper());

        // Reserved keywords in oleDB
        private readonly string reservedStringPattern = @"^[\/\\\$\%]+$|^.*[<>].*$";

        private string WarningIconPath { get; set; }

        public string Name => Properties.Resources.Microsoft_plugin_indexer_name;

        public string Description => Properties.Resources.Microsoft_plugin_indexer_plugin_description;

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>()
        {
            new PluginAdditionalOption()
            {
                Key = DisableDriveDetectionWarning,
                DisplayLabel = Properties.Resources.disable_drive_detection_warning,
                Value = false,
            },
        };

        private IContextMenu _contextMenuLoader;
        private bool disposedValue;

        // To save the configurations of plugins
        public void Save()
        {
            _storage.Save();
        }

        // This function uses the Windows indexer and returns the list of results obtained
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "We want to keep the process alive but will log the exception")]
        public List<Result> Query(Query query, bool isFullQuery)
        {
            var results = new List<Result>();

            if (!string.IsNullOrEmpty(query.Search))
            {
                var searchQuery = query.Search;
                if (_settings.MaxSearchCount <= 0)
                {
                    _settings.MaxSearchCount = 30;
                }

                var regexMatch = Regex.Match(searchQuery, reservedStringPattern);

                if (!regexMatch.Success)
                {
                    try
                    {
                        if (_driveDetection.DisplayWarning())
                        {
                            results.Add(new Result
                            {
                                Title = Properties.Resources.Microsoft_plugin_indexer_drivedetectionwarning,
                                SubTitle = Properties.Resources.Microsoft_plugin_indexer_disable_warning_in_settings,
                                IcoPath = WarningIconPath,
                                Action = e =>
                                {
                                    try
                                    {
                                        Process.Start(GetWindowsSearchSettingsProcessInfo());
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Exception($"Unable to launch Windows Search Settings: {ex.Message}", ex, GetType());
                                    }

                                    return true;
                                },
                            });
                        }

                        // This uses the Microsoft.Search.Interop assembly
                        var searchManager = new CSearchManager();
                        var searchResultsList = _api.Search(searchQuery, searchManager, maxCount: _settings.MaxSearchCount).ToList();

                        // If the delayed execution query is not required (since the SQL query is fast) return empty results
                        if (searchResultsList.Count == 0 && isFullQuery)
                        {
                            return new List<Result>();
                        }

                        foreach (var searchResult in searchResultsList)
                        {
                            var path = searchResult.Path;

                            // Using CurrentCulture since this is user facing
                            var toolTipTitle = string.Format(CultureInfo.CurrentCulture, "{0} : {1}", Properties.Resources.Microsoft_plugin_indexer_name, searchResult.Title);
                            var toolTipText = string.Format(CultureInfo.CurrentCulture, "{0} : {1}", Properties.Resources.Microsoft_plugin_indexer_path, path);
                            string workingDir = null;
                            if (_settings.UseLocationAsWorkingDir)
                            {
                                workingDir = _fileSystem.Path.GetDirectoryName(path);
                            }

                            Result r = new Result();
                            r.Title = searchResult.Title;
                            r.SubTitle = Properties.Resources.Microsoft_plugin_indexer_subtitle_header + ": " + path;
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
                                        WorkingDirectory = workingDir,
                                    });
                                    hide = true;
                                }
                                catch (Win32Exception)
                                {
                                    var name = $"Plugin: {_context.CurrentPluginMetadata.Name}";
                                    var msg = Properties.Resources.Microsoft_plugin_indexer_file_open_failed;
                                    _context.API.ShowMsg(name, msg, string.Empty);
                                    hide = false;
                                }

                                return hide;
                            };
                            r.ContextData = searchResult;

                            // If the result is a directory, then it's display should show a directory.
                            if (_fileSystem.Directory.Exists(path))
                            {
                                r.QueryTextDisplay = path;
                            }

                            results.Add(r);
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // The connection has closed, internal error of ExecuteReader()
                        // Not showing this exception to the users
                    }
                    catch (Exception ex)
                    {
                        Log.Exception("Something failed", ex, GetType());
                    }
                }
            }

            return results;
        }

        // This function uses the Windows indexer and returns the list of results obtained. This version is required to implement the interface
        public List<Result> Query(Query query)
        {
            // All plugins have to implement IPlugin interface. We return empty collection as we do not want any computation with constant search plugins.
            return new List<Result>();
        }

        public void Init(PluginInitContext context)
        {
            // initialize the context of the plugin
            _context = context;
            _contextMenuLoader = new ContextMenuLoader(context);
            _storage = new PluginJsonStorage<IndexerSettings>();
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

        private void OnThemeChanged(Theme currentTheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        // Set the Plugin Title
        public string GetTranslatedPluginTitle()
        {
            return Properties.Resources.Microsoft_plugin_indexer_plugin_name;
        }

        // Set the plugin Description
        public string GetTranslatedPluginDescription()
        {
            return Properties.Resources.Microsoft_plugin_indexer_plugin_description;
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            return _contextMenuLoader.LoadContextMenus(selectedResult);
        }

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            var option = settings.AdditionalOptions.FirstOrDefault(x => x.Key == DisableDriveDetectionWarning);
            _driveDetection.IsDriveDetectionWarningCheckBoxSelected = option == null ? false : option.Value;
        }

        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        // Returns the Process Start Information for the new Windows Search Settings
        public static ProcessStartInfo GetWindowsSearchSettingsProcessInfo()
        {
            var ps = new ProcessStartInfo("ms-settings:cortana-windowssearch")
            {
                UseShellExecute = true,
                Verb = "open",
            };

            return ps;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
