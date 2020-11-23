// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using ManagedCommon;
using Microsoft.Plugin.Registry.Classes;
using Microsoft.Plugin.Registry.Helper;
using Wox.Plugin;

namespace Microsoft.Plugin.Registry
{
    // TEST:
    // Computer\HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\Shell\Bags\31\Shell\{5C4F28B5-F869-4E84-8E60-F11DB97C5CC7}
    // hklm/so/mi/wi/sh/ba/31/sh/{5c

    // Finished:
    // - shortcuts for main keys (e.g. HKLM)
    // - support search for full main keys not only shortcuts
    // - show all root keys when a user type only a part of "HKEY"
    // - match keys they start with shortcut + search
    // - list of found keys
    // - result of found keys
    // - auto replace "/" with "\"
    // - always case-insensitive
    // - command: open direct in regedit.exe
    // - command: copy key or value name to clipboard

    // TODO:
    // - step into key values via ???
    // - allow search by value name (search after ':')
    // - multi-language
    // - benchmark
    // - unit-tests
    public class Main : IPlugin, IContextMenu, IDisposable
    {
        private PluginInitContext? _context;
        private string _defaultIconPath;
        private bool _disposed;

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
            if (query?.Search.Length == 0)
            {
                return new List<Result>(0);
            }

            var search = query?.Search.Replace('/', '\\') ?? string.Empty;
            if (search.Length == 0)
            {
                return new List<Result>(0);
            }

            var (mainKey, path) = RegistryHelper.GetRegistryMainKey(search.TrimEnd(':'));
            if (mainKey is null)
            {
                return search.StartsWith("HKEY", StringComparison.InvariantCultureIgnoreCase) == true
                    ? ResultHelper.GetResultList(RegistryHelper.GetAllMainKeys(), _defaultIconPath)
                    : new List<Result>(0);
            }

            ICollection<RegistryEntry> list = new Collection<RegistryEntry>();

            if (!(mainKey is null))
            {
                list = RegistryHelper.SearchForSubKey(mainKey, path);
            }

            return list.Count switch
            {
                0 => new List<Result>(0),
                1 when search.EndsWith(':') => ResultHelper.GetValuesFromKey(list.FirstOrDefault().Key, _defaultIconPath),
                _ => ResultHelper.GetResultList(list, _defaultIconPath),
            };
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
                list.Add(new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = "Copy key to clipboard",
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
