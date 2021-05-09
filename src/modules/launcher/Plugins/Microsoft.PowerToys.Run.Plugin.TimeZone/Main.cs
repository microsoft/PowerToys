// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ManagedCommon;
using Microsoft.PowerToys.Run.Plugin.TimeZone.Properties;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.TimeZone
{
    public class Main : IPlugin, IPluginI18n, IDisposable
    {
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

        public string Name => Resources.PluginTitle;

        public string Description => Resources.PluginDescription;

        /// <summary>
        /// Initializes a new instance of the <see cref="Main"/> class.
        /// </summary>
        public Main()
        {
            _assemblyName = Assembly.GetExecutingAssembly().GetName().Name ?? GetTranslatedPluginTitle();
            _defaultIconPath = "Images/reg.light.png";
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
        }

        /// <summary>
        /// Return a filtered list, based on the given query
        /// </summary>
        /// <param name="query">The query to filter the list</param>
        /// <returns>A filtered list, can be empty when nothing was found</returns>
        public List<Result> Query(Query query)
        {
            if (query is null)
            {
                return new List<Result>(0);
            }

            var timeZones = TimeZoneInfo.GetSystemTimeZones();
            var results = new List<Result>(timeZones.Count);
            var utcNow = DateTime.UtcNow;

            foreach (var timeZone in timeZones)
            {
                if (!timeZone.DaylightName.Contains(query.Search, StringComparison.InvariantCultureIgnoreCase)
                && !timeZone.DaylightName.Contains(query.Search, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                var title = timeZone.SupportsDaylightSavingTime && timeZone.IsDaylightSavingTime(utcNow)
                    ? timeZone.DaylightName
                    : timeZone.StandardName;

                var timeInTimeZone = TimeZoneInfo.ConvertTime(utcNow, timeZone);

                var result = new Result
                {
                    Title = title,
                    SubTitle = $"Time: {timeInTimeZone:HH:mm:ss}",
                    ToolTipData = new ToolTipData(title, $"Offset: {timeZone.BaseUtcOffset}"),
                };

                results.Add(result);
            }

            return results.OrderBy(result => result.Title).ToList();
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
                ? "Images/reg.light.png"
                : "Images/reg.dark.png";
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

        /// <inheritdoc/>
        public string GetTranslatedPluginTitle()
        {
            return Resources.PluginTitle;
        }

        /// <inheritdoc/>
        public string GetTranslatedPluginDescription()
        {
            return Resources.PluginDescription;
        }
    }
}
