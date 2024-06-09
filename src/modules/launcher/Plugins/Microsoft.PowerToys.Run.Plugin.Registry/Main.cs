// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ManagedCommon;
using Microsoft.PowerToys.Run.Plugin.Registry.Classes;
using Microsoft.PowerToys.Run.Plugin.Registry.Helper;
using Microsoft.PowerToys.Run.Plugin.Registry.Properties;
using Wox.Plugin;

[assembly: InternalsVisibleTo("Microsoft.PowerToys.Run.Plugin.Registry.UnitTests")]

namespace Microsoft.PowerToys.Run.Plugin.Registry
{
    /// <summary>
    /// Main class of this plugin that implement all used interfaces
    /// </summary>
    public class Main : IPlugin, IContextMenu, IPluginI18n, IDisposable
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

        public static string PluginID => "303417D927BF4C97BCFFC78A123BE0C8";

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
            if (query?.Search is null)
            {
                return new List<Result>(0);
            }

            var searchForValueName = QueryHelper.GetQueryParts(query.Search, out var queryKey, out var queryValueName);

            var (baseKeyList, subKey) = RegistryHelper.GetRegistryBaseKey(queryKey);
            if (baseKeyList is null)
            {
                // no base key found
                return ResultHelper.GetResultList(RegistryHelper.GetAllBaseKeys(), _defaultIconPath);
            }
            else if (baseKeyList.Count() == 1)
            {
                // only one base key was found -> start search for the sub-key
                var list = RegistryHelper.SearchForSubKey(baseKeyList.First(), subKey);

                // when only one sub-key was found and a user search for values ("\\")
                // show the filtered list of values of one sub-key
                if (searchForValueName && list.Count == 1)
                {
                    return ResultHelper.GetValuesFromKey(list.First().Key, _defaultIconPath, queryValueName);
                }

                return ResultHelper.GetResultList(list, _defaultIconPath);
            }
            else if (baseKeyList.Count() > 1)
            {
                // more than one base key was found -> show results
                return ResultHelper.GetResultList(baseKeyList.Select(found => new RegistryEntry(found)), _defaultIconPath);
            }

            return new List<Result>();
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

            if (_context != null && _context.API != null)
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
