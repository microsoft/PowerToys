// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using ManagedCommon;
using Microsoft.Plugin.Registry.Classes;
using Microsoft.Plugin.Registry.Helper;
using Wox.Plugin;

namespace Microsoft.Plugin.Registry
{
    /*
     * TODO:
     * - allow search by value name (search after ':')
     * - multi-language
     * - benchmark
     * - unit-tests
     */

    public class Main : IPlugin, IContextMenu, IDisposable /* ,IResultUpdated */
    {
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

        /*
        * private Query? _query;
        * /// <summary>
        * /// This event is triggered when the result should be updated without any user interaction
        * /// </summary>
        * public event ResultUpdatedEventHandler? ResultsUpdated;
        */

        public Main()
            => _defaultIconPath = "Images/reg.light.png";

        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(_context.API.GetCurrentTheme());
        }

        public List<Result> Query(Query query)
        {
            /* _query = query; */

            // Any main registry key have more than two characters
            if (query is null || query.Search.Length < 2)
            {
                return new List<Result>(0);
            }

            var search = query.Search.EndsWith("\\\\", StringComparison.InvariantCultureIgnoreCase)
                ? query.Search.TrimEnd('\\')
                : query.Search;

            var (mainKey, path) = RegistryHelper.GetRegistryMainKey(search);
            if (mainKey is null)
            {
                return query.Search.StartsWith("HKEY", StringComparison.InvariantCultureIgnoreCase)
                    ? ResultHelper.GetResultList(RegistryHelper.GetAllMainKeys(), _defaultIconPath)
                    : new List<Result>(0);
            }

            var list = RegistryHelper.SearchForSubKey(mainKey, path);

            if (query.Search.EndsWith("\\\\", StringComparison.InvariantCultureIgnoreCase))
            {
                var firstEntry = list.FirstOrDefault(found => found.Key != null
                                                            && found.Key.Name.StartsWith(search, StringComparison.InvariantCultureIgnoreCase));
                if (!(firstEntry is null))
                {
                    return ResultHelper.GetValuesFromKey(firstEntry.Key, _defaultIconPath);
                }
            }

            return ResultHelper.GetResultList(list, _defaultIconPath);
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            if (!(selectedResult?.ContextData is RegistryEntry entry))
            {
                return new List<ContextMenuResult>(0);
            }

            var list = new List<ContextMenuResult>();

            if (entry.Key?.Name == selectedResult.Title)
            {
                /* doesn't work, surface don't refresh the result list
                list.Add(new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = "Open registry key",
                    Glyph = "\xE71E",                       // E71E => Zoom
                    FontFamily = "Segoe MDL2 Assets",
                    AcceleratorKey = Key.O,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Action = (hideAfterSelectResult) =>
                    {
                        ResultsUpdated?.Invoke(this, new ResultUpdatedEventArgs(ResultHelper.GetValuesFromKey(entry.Key, _defaultIconPath))
                        {
                            Query = _query,   // WOX crash, when query is null
                        });
                    return false;            // return value currently not implemented in WOX
                    },
                });
                */

                list.Add(new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = "Copy registry key to clipboard",
                    Glyph = "\xF0E3",                       // E70F => ClipboardList
                    FontFamily = "Segoe MDL2 Assets",
                    AcceleratorKey = Key.C,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Action = _ => ContextMenuHelper.TryToCopyToClipBoard(entry.Key?.Name ?? entry.KeyPath),
                });
            }
            else
            {
                list.Add(new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = "Copy value name to clipboard",
                    Glyph = "\xF0E3",                       // E70F => ClipboardList
                    FontFamily = "Segoe MDL2 Assets",
                    AcceleratorKey = Key.N,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Action = _ => ContextMenuHelper.TryToCopyToClipBoard(selectedResult.Title),
                });
            }

            list.Add(new ContextMenuResult
            {
                PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                Title = "Open key in registry editor",
                Glyph = "\xE70F",                       // E70F => Edit (Pencil)
                FontFamily = "Segoe MDL2 Assets",
                AcceleratorKey = Key.Enter,
                AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                Action = _ => ContextMenuHelper.TryToOpenInRegistryEditor(entry),
            });

            return list;
        }

        private void OnThemeChanged(Theme oldtheme, Theme newTheme)
            => UpdateIconPath(newTheme);

        private void UpdateIconPath(Theme theme)
            => _defaultIconPath = theme == Theme.Light || theme == Theme.HighContrastWhite ? "Images/reg.light.png" : "Images/reg.dark.png";

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

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
    }
}
