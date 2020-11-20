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
using Microsoft.Plugin.Registry.Helper;
using Microsoft.Win32;
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
    // - always case-intensitive
    // - make as PT plugin
    // - command: open direct in regedit.exe
    // - command: copy key/name/value to clipboard
    // - show key values via ':'

    // TODO:
    // - allow search by value name (search after ':')
    // - reduce used of strings, use RegisterKey instead
    // - avoid use of tuples use key value instead
    // - simple key-walker with full keys
    // - extended key-walker with only parts of the keys
    // - multi-language
    // - cache results ?
    // - benchmark
    // - unittests
    // - dark/light theme switch
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
            var search = query?.Search.Replace('/', '\\') ?? string.Empty;

            ICollection<(string, RegistryKey?, Exception?)> list = new Collection<(string, RegistryKey?, Exception?)>();

            var (mainKey, path) = RegistryHelper.GetRegistryKey(search.TrimEnd(':'));

            if (mainKey is null && search.StartsWith("HKEY", StringComparison.InvariantCultureIgnoreCase))
            {
                list = RegistryHelper.GetAllMainKeys();
            }
            else if (!(mainKey is null))
            {
                list = RegistryHelper.SearchForSubKey(mainKey, path);
            }

            return list.Count switch
            {
                0 => new List<Result> { new Result { Title = $"{query?.Search} Key not found" } },
                1 when search.EndsWith(':') => ResultHelper.GetValuesFromKey(list.FirstOrDefault().Item2, _defaultIconPath),
                _ => ResultHelper.GetResultList(list, _defaultIconPath),
            };
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            if (!(selectedResult?.ContextData is RegistryKey key))
            {
                return new List<ContextMenuResult>(0);
            }

            var list = new List<ContextMenuResult>();

            if (key.Name == selectedResult.Title)
            {
                list.Add(new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = "Copy key to clipboard",
                    Glyph = "\xF0E3",                       // E70F => ClipboardList
                    FontFamily = "Segoe MDL2 Assets",
                    AcceleratorKey = Key.C,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Action = _ => ContextMenuHelper.CopyToClipBoard(key.Name),
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
                    Action = _ => ContextMenuHelper.CopyToClipBoard(selectedResult.Title),
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
                Action = _ => ContextMenuHelper.OpenInRegistryEditor(key.Name),
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
