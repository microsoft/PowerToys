// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using ManagedCommon;
using Microsoft.PowerToys.Run.Plugin.TimeZone.Classes;
using Microsoft.PowerToys.Run.Plugin.TimeZone.Helper;
using Microsoft.PowerToys.Run.Plugin.TimeZone.Properties;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.TimeZone
{
    /// <summary>
    /// A power launcher plugin to search across time zones.
    /// </summary>
    public class Main : IPlugin, IContextMenu, IPluginI18n, ISettingProvider, IDisposable
    {
        /// <summary>
        /// The name of this assembly
        /// </summary>
        private readonly string _assemblyName;

        /// <summary>
        /// The settings for this plugin.
        /// </summary>
        private readonly TimeZoneSettings _timeZoneSettings;

        /// <summary>
        /// The initial context for this plugin (contains API and meta-data)
        /// </summary>
        private PluginInitContext? _context;

        /// <summary>
        /// The path to the icon for each result
        /// </summary>
        private string _defaultIconPath;

        /// <summary>
        /// Indicate that the plugin is disposed
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// A class that contain all possible time zones.
        /// </summary>
        private TimeZoneList? _timeZoneList;

        /// <summary>
        /// Initializes a new instance of the <see cref="Main"/> class.
        /// </summary>
        public Main()
        {
            _assemblyName = Assembly.GetExecutingAssembly().GetName().Name ?? GetTranslatedPluginTitle();
            _defaultIconPath = "Images/timeZone.light.png";
            _timeZoneSettings = new TimeZoneSettings();
        }

        /// <summary>
        /// Gets the localized name.
        /// </summary>
        public string Name
        {
            get { return Resources.PluginTitle; }
        }

        /// <summary>
        /// Gets the localized description.
        /// </summary>
        public string Description
        {
            get { return Resources.PluginDescription; }
        }

        /// <summary>
        /// Gets the additional options for this plugin.
        /// </summary>
        public IEnumerable<PluginAdditionalOption> AdditionalOptions
        {
            get { return TimeZoneSettings.GetAdditionalOptions(); }
        }

        /// <summary>
        /// Initialize the plugin with the given <see cref="PluginInitContext"/>
        /// </summary>
        /// <param name="context">The <see cref="PluginInitContext"/> for this plugin</param>
        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(_context.API.GetCurrentTheme());

            _timeZoneList = JsonHelper.ReadAllPossibleTimeZones();

            TranslationHelper.TranslateAllSettings(_timeZoneList);
        }

        /// <summary>
        /// Return a filtered list, based on the given query
        /// </summary>
        /// <param name="query">The query to filter the list</param>
        /// <returns>A filtered list, can be empty when nothing was found</returns>
        public List<Result> Query(Query query)
        {
            if (_timeZoneList?.TimeZones is null)
            {
                return new List<Result>(0);
            }

            if (query is null)
            {
                return new List<Result>(0);
            }

            var results = ResultHelper.GetResults(_timeZoneList.TimeZones, _timeZoneSettings, query, _defaultIconPath);
            return results.ToList();
        }

        /// <summary>
        /// Return a list context menu entries for a given <see cref="Result"/> (shown at the right side of the result)
        /// </summary>
        /// <param name="selectedResult">The <see cref="Result"/> for the list with context menu entries</param>
        /// <returns>A list context menu entries</returns>
        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            return ContextMenuHelper.GetContextMenu(selectedResult, _assemblyName);
        }

        /// <summary>
        /// Change all theme-based elements (typical called when the plugin theme has changed)
        /// </summary>
        /// <param name="oldtheme">The old <see cref="Theme"/></param>
        /// <param name="newTheme">The new <see cref="Theme"/></param>
        private void OnThemeChanged(Theme oldtheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        /// <summary>
        /// Update all icons (typical called when the plugin theme has changed)
        /// </summary>
        /// <param name="theme">The new <see cref="Theme"/> for the icons</param>
        private void UpdateIconPath(Theme theme)
        {
            _defaultIconPath = theme == Theme.Light || theme == Theme.HighContrastWhite
                ? "Images/timeZone.light.png"
                : "Images/timeZone.dark.png";
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Wrapper method for <see cref="Dispose"/> that dispose additional objects and events form the plugin itself
        /// </summary>
        /// <param name="disposing">Indicate that the plugin is disposed</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed || !disposing)
            {
                return;
            }

            if (!(_context is null))
            {
                _context.API.ThemeChanged -= OnThemeChanged;
            }

            _disposed = true;
        }

        /// <summary>
        /// Return the translated plugin title.
        /// </summary>
        /// <returns>A translated plugin title.</returns>
        public string GetTranslatedPluginTitle()
        {
            return Resources.PluginTitle;
        }

        /// <summary>
        /// Return the translated plugin description.
        /// </summary>
        /// <returns>A translated plugin description.</returns>
        public string GetTranslatedPluginDescription()
        {
            return Resources.PluginDescription;
        }

        /// <summary>
        /// Return a additional setting panel for this plugin.
        /// </summary>
        /// <returns>A additional setting panel.</returns>
        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Update the plugin settings
        /// </summary>
        /// <param name="settings">The settings for all power launcher plugin.</param>
        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            _timeZoneSettings.UpdateSettings(settings);
        }
    }
}
