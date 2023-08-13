// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ManagedCommon;
using Microsoft.PowerToys.Run.Plugin.WindowsSettings.Helper;
using Microsoft.PowerToys.Run.Plugin.WindowsSettings.Properties;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.WindowsSettings
{
    /// <summary>
    /// Main class of this plugin that implement all used interfaces.
    /// </summary>
    public class Main : IPlugin, IContextMenu, IPluginI18n, IDisposable
    {
        /// <summary>
        /// The path to the symbol for a light theme.
        /// </summary>
        private const string _lightSymbol = "Images/WindowsSettings.light.png";

        /// <summary>
        /// The path to the symbol for a dark theme.
        /// </summary>
        private const string _darkSymbol = "Images/WindowsSettings.dark.png";

        /// <summary>
        /// The name of this assembly.
        /// </summary>
        private readonly string _assemblyName;

        /// <summary>
        /// The initial context for this plugin (contains API and meta-data).
        /// </summary>
        private PluginInitContext? _context;

        /// <summary>
        /// The path to the icon for each result.
        /// </summary>
        private string _defaultIconPath;

        /// <summary>
        /// Indicate that the plugin is disposed.
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// A class that contain all possible windows settings.
        /// </summary>
        private WindowsSettings? _windowsSettings;

        /// <summary>
        /// Initializes a new instance of the <see cref="Main"/> class.
        /// </summary>
        public Main()
        {
            _assemblyName = Assembly.GetExecutingAssembly().GetName().Name ?? Name;
            _defaultIconPath = _lightSymbol;
        }

        /// <summary>
        /// Gets the localized name.
        /// </summary>
        public string Name => Resources.PluginTitle;

        /// <summary>
        /// Gets the localized description.
        /// </summary>
        public string Description => Resources.PluginDescription;

        /// <summary>
        /// Gets the plugin ID for validation
        /// </summary>
        public static string PluginID => "5043CECEE6A748679CBE02D27D83747A";

        /// <summary>
        /// Initialize the plugin with the given <see cref="PluginInitContext"/>.
        /// </summary>
        /// <param name="context">The <see cref="PluginInitContext"/> for this plugin.</param>
        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(_context.API.GetCurrentTheme());

            _windowsSettings = JsonSettingsListHelper.ReadAllPossibleSettings();

            UnsupportedSettingsHelper.FilterByBuild(_windowsSettings);

            TranslationHelper.TranslateAllSettings(_windowsSettings);
            WindowsSettingsPathHelper.GenerateSettingsPathValues(_windowsSettings);
        }

        /// <summary>
        /// Return a filtered list, based on the given query.
        /// </summary>
        /// <param name="query">The query to filter the list.</param>
        /// <returns>A filtered list, can be empty when nothing was found.</returns>
        public List<Result> Query(Query query)
        {
            if (_windowsSettings?.Settings is null)
            {
                return new List<Result>(0);
            }

            var filteredList = _windowsSettings.Settings
                .Where(Predicate)
                .OrderBy(found => found.Name);

            var newList = ResultHelper.GetResultList(filteredList, query.Search, _defaultIconPath);
            return newList;

            bool Predicate(WindowsSetting found)
            {
                if (string.IsNullOrWhiteSpace(query.Search))
                {
                    // If no search string is entered skip query comparison.
                    return true;
                }

                if (found.Name.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase))
                {
                    return true;
                }

                if (!(found.Areas is null))
                {
                    foreach (var area in found.Areas)
                    {
                        // Search for areas on normal queries.
                        if (area.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase))
                        {
                            return true;
                        }

                        // Search for Area only on queries with action char.
                        if (area.Contains(query.Search.Replace(":", string.Empty), StringComparison.CurrentCultureIgnoreCase)
                        && query.Search.EndsWith(":", StringComparison.CurrentCultureIgnoreCase))
                        {
                            return true;
                        }
                    }
                }

                if (!(found.AltNames is null))
                {
                    foreach (var altName in found.AltNames)
                    {
                        if (altName.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase))
                        {
                            return true;
                        }
                    }
                }

                // Search by key char '>' for app name and settings path
                if (query.Search.Contains('>'))
                {
                    return ResultHelper.FilterBySettingsPath(found, query.Search);
                }

                return false;
            }
        }

        /// <summary>
        /// Return a list context menu entries for a given <see cref="Result"/> (shown at the right side of the result).
        /// </summary>
        /// <param name="selectedResult">The <see cref="Result"/> for the list with context menu entries.</param>
        /// <returns>A list context menu entries.</returns>
        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            return ContextMenuHelper.GetContextMenu(selectedResult, _assemblyName);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Wrapper method for <see cref="Dispose"/> that dispose additional objects and events form the plugin itself.
        /// </summary>
        /// <param name="disposing">Indicate that the plugin is disposed.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed || !disposing)
            {
                return;
            }

            if (_context != null && _context.API != null)
            {
                _context.API.ThemeChanged -= OnThemeChanged;
            }

            _disposed = true;
        }

        /// <summary>
        /// Gets the localized name.
        /// </summary>
        public string GetTranslatedPluginTitle()
        {
            return Name;
        }

        /// <summary>
        /// Gets the localized description.
        /// </summary>
        public string GetTranslatedPluginDescription()
        {
            return Description;
        }

        /// <summary>
        /// Change all theme-based elements (typical called when the plugin theme has changed).
        /// </summary>
        /// <param name="oldtheme">The old <see cref="Theme"/>.</param>
        /// <param name="newTheme">The new <see cref="Theme"/>.</param>
        private void OnThemeChanged(Theme oldtheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        /// <summary>
        /// Update all icons (typical called when the plugin theme has changed).
        /// </summary>
        /// <param name="theme">The new <see cref="Theme"/> for the icons.</param>
        private void UpdateIconPath(Theme theme)
        {
            _defaultIconPath = theme == Theme.Light || theme == Theme.HighContrastWhite
                ? _lightSymbol
                : _darkSymbol;
        }
    }
}
