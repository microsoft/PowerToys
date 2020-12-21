// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ManagedCommon;
using Microsoft.Plugin.Registry.Classes;
using Microsoft.Plugin.Registry.Helper;
using Microsoft.Plugin.Registry.Properties;
using Wox.Plugin;

[assembly: InternalsVisibleTo("Microsoft.Plugin.Registry.UnitTest")]

namespace Microsoft.Plugin.Registry
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

            List<Result> result;

            // The plugin is only triggered when the "ActionKeyword" is "HK" (case-insensitive).
            // To make the develop of this plugin easier we put the "Query.ActionKeyword" and the "Query.Search" together,
            // this allow us to use e.g. "HKLM" instead of "LM".
            var serachFor = query.ActionKeyword + query.Search;

            // WOX hack:
            // This Plugin override the "Result.QueryTextDisplay" of each result, this is need to allow to work with short-hand base registry keys (e.g. HKLM).
            // The WOX plugin use the changed "Result.QueryTextDisplay" and show "Query.ActionKeyword" + Space + "Result.QueryTextDisplay".
            // We can't use "Query.RawQuery" because it have the same content ("Query.ActionKeyword" + Space + "Result.QueryTextDisplay").
            // So we clear the action keyword to make sure it is not used for the display and the query.
            query.ActionKeyword = string.Empty;

            var searchForValueName = QueryHelper.GetQueryParts(serachFor, out var queryKey, out var queryValueName);

            var (baseKeyList, subKey) = RegistryHelper.GetRegistryBaseKey(queryKey);
            if (baseKeyList is null)
            {
                // no base key found
                result = ResultHelper.GetResultList(RegistryHelper.GetAllBaseKeys(), _defaultIconPath);
            }
            else if (baseKeyList.Count() > 1)
            {
                // more than one base key was found -> show results
                result = ResultHelper.GetResultList(baseKeyList.Select(found => new RegistryEntry(found)), _defaultIconPath);
            }
            else
            {
                // only one base key found -> start search for the sub key
                var list = RegistryHelper.SearchForSubKey(baseKeyList.First(), subKey);

                if (!searchForValueName)
                {
                    return ResultHelper.GetResultList(list, _defaultIconPath);
                }

                queryKey = QueryHelper.GetKeyWithLongBaseKey(queryKey);

                var firstEntry = list.FirstOrDefault(found => found.Key != null
                                                            && found.Key.Name.StartsWith(queryKey, StringComparison.InvariantCultureIgnoreCase));
                result = firstEntry is null
                    ? ResultHelper.GetResultList(list, _defaultIconPath)
                    : ResultHelper.GetValuesFromKey(firstEntry.Key, _defaultIconPath, queryValueName);
            }

            return result;
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
            => UpdateIconPath(newTheme);

        /// <summary>
        /// Update all icons (typical called when the plugin theme has changed)
        /// </summary>
        /// <param name="theme">The new <see cref="Theme"/> for the icons</param>
        private void UpdateIconPath(Theme theme)
            => _defaultIconPath = theme == Theme.Light || theme == Theme.HighContrastWhite ? "Images/reg.light.png" : "Images/reg.dark.png";

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
            => Resources.PluginTitle;

        /// <inheritdoc/>
        public string GetTranslatedPluginDescription()
            => Resources.PluginDescription;
    }
}
