// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManagedCommon;
using Microsoft.PowerToys.Run.Plugin.WindowsRegistry.Helper;
using Microsoft.PowerToys.Run.Plugin.WindowsSettings.Classes;
using Microsoft.PowerToys.Run.Plugin.WindowsSettings.Helper;
using Microsoft.PowerToys.Run.Plugin.WindowsSettings.Properties;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.WindowsSettings
{
    /// <summary>
    /// Main class of this plugin that implement all used interfaces
    /// </summary>
    public class Main : IPlugin, IContextMenu, IPluginI18n, IDisposable
    {
        /// <summary>
        /// The path to the symbol for a light theme
        /// </summary>
        private const string _lightSymbol = "Images/WindowsSettings.light.png";

        /// <summary>
        /// The path to the symbol for a dark theme
        /// </summary>
        private const string _darkSymbol = "Images/WindowsSettings.dark.png";

        /// <summary>
        /// The name of the file that contains all settings for the query
        /// </summary>
        private const string _settingsFile = "WindowsSettings.json";

        /// <summary>
        /// The name of this assembly
        /// </summary>
        private readonly string _assemblyName;

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

        private IEnumerable<WindowsSetting>? _settingsList;

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
        /// Initialize the plugin with the given <see cref="PluginInitContext"/>
        /// </summary>
        /// <param name="context">The <see cref="PluginInitContext"/> for this plugin</param>
        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(_context.API.GetCurrentTheme());

            var assembly = Assembly.GetExecutingAssembly();
            var type = assembly.GetTypes().FirstOrDefault(x => x.Name == nameof(Main));

            var resourceName = $"{type?.Namespace}.{_settingsFile}";

            try
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream is null)
                {
                    throw new Exception("stream is null");
                }

                var options = new JsonSerializerOptions();
                options.Converters.Add(new JsonStringEnumConverter());

                using var reader = new StreamReader(stream);
                var text = reader.ReadToEnd();

                _settingsList = JsonSerializer.Deserialize<IEnumerable<WindowsSetting>>(text, options);
            }
            catch (Exception exception)
            {
                Log.Exception("Error loading settings JSON file", exception, typeof(Main));
            }

            if (_settingsList is null)
            {
                return;
            }

            TranslateAllSettings();

            var currentWindowsVersion = RegistryHelper.GetCurrentWindowsVersion();

            // remove deprecated settings and settings that are for a higher Windows versions
            _settingsList = _settingsList.Where(found
                => (found.DeprecatedInVersion == null || currentWindowsVersion < found.DeprecatedInVersion)
                && (found.IntroducedInVersion == null || currentWindowsVersion >= found.IntroducedInVersion));

            // sort settings list
            _settingsList = _settingsList.OrderBy(found => found.Name);
        }

        /// <summary>
        /// Return a filtered list, based on the given query
        /// </summary>
        /// <param name="query">The query to filter the list</param>
        /// <returns>A filtered list, can be empty when nothing was found</returns>
        public List<Result> Query(Query query)
        {
            if (_settingsList is null)
            {
                return new List<Result>(0);
            }

            var filteredList = _settingsList
                .Where(Predicate)
                .OrderBy(found => found.Name);

            var newList = ResultHelper.GetResultList(filteredList, query.Search, _defaultIconPath);
            return newList;

            bool Predicate(WindowsSetting found)
            {
                if (found.Name.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase))
                {
                    return true;
                }

                if (found.Area.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase))
                {
                    return true;
                }

                if (found.AltNames is null)
                {
                    return false;
                }

                foreach (var altName in found.AltNames)
                {
                    if (altName.Contains(query.Search, StringComparison.CurrentCultureIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
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
                ? _lightSymbol
                : _darkSymbol;
        }

        /// <summary>
        /// Translate all setting entires
        /// </summary>
        private void TranslateAllSettings()
        {
            if (_settingsList is null)
            {
                return;
            }

            foreach (var settings in _settingsList)
            {
                var area = Resources.ResourceManager.GetString($"Area{settings.Area}");
                var name = Resources.ResourceManager.GetString(settings.Name);

                if (string.IsNullOrEmpty(area))
                {
                    Log.Warn($"Resource string for [Area{settings.Area}] not found", typeof(Main));
                }

                if (string.IsNullOrEmpty(name))
                {
                    Log.Warn($"Resource string for [{settings.Name}] not found", typeof(Main));
                }

                settings.Area = area ?? settings.Area;
                settings.Name = name ?? settings.Name;

                if (!string.IsNullOrEmpty(settings.Note))
                {
                    var note = Resources.ResourceManager.GetString(settings.Note);
                    settings.Note = note ?? settings.Note;

                    if (string.IsNullOrEmpty(note))
                    {
                        Log.Warn($"Resource string for [{settings.Note}] not found", typeof(Main));
                    }
                }

                if (!(settings.AltNames is null) && settings.AltNames.Any())
                {
                    var translatedAltNames = new Collection<string>();

                    foreach (var altName in settings.AltNames)
                    {
                        var translatedAltName = Resources.ResourceManager.GetString(altName);

                        if (string.IsNullOrEmpty(translatedAltName))
                        {
                            Log.Warn($"Resource string for [{altName}] not found", typeof(Main));
                        }

                        translatedAltNames.Add(translatedAltName ?? altName);
                    }

                    settings.AltNames = translatedAltNames;
                }
            }
        }
    }
}
