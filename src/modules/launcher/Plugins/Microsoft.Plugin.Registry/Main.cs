// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Input;
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
    // - command: copy key to clipboard

    // TODO:
    // - command: copy value to clipboard
    // - reduce used of strings, use RegisterKey instead
    // - avoid use of tuples use key value instead
    // - simple key-walker with full keys
    // - extended key-walker with only parts of the keys
    // - multi-language
    // - cache results ?
    // - benchmark
    // - unittests
    public class Main : IPlugin, IContextMenu
    {
        public void Init(PluginInitContext context)
        {
        }

        public List<Result> Query(Query query)
        {
            var search = query?.Search.Replace('/', '\\') ?? string.Empty;

            ICollection<(string, RegistryKey?, Exception?)> list = new Collection<(string, RegistryKey?, Exception?)>();

            var (mainKey, path) = RegistryHelper.GetRegistryKey(search);

            if (mainKey is null && search.StartsWith("HKEY", StringComparison.InvariantCultureIgnoreCase))
            {
                list = RegistryHelper.GetAllMainKeys();
            }
            else if (!(mainKey is null))
            {
                list = RegistryHelper.SerachForSubKey(mainKey, path);
            }

            return list.Count == 0
                ? new List<Result> { new Result { Title = $"{query?.Search} Key not found" } }
                : PrepareResults(list);
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
            => new List<ContextMenuResult>
            {
                new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = "Open in registry editor",
                    Glyph = "\xE70F",                       // E70F => Edit (Pencil)
                    FontFamily = "Segoe MDL2 Assets",
                    AcceleratorKey = Key.Enter,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Action = _ => ContextMenuHelper.OpenInRegistryEditor(selectedResult),
                },

                new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = "Copy key to clipboard",
                    Glyph = "\xF0E3",                       // E70F => ClipboardList
                    FontFamily = "Segoe MDL2 Assets",
                    AcceleratorKey = Key.C,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Action = _ => ContextMenuHelper.CopyToClipBoard(selectedResult),
                },
            };

        private static List<Result> PrepareResults(ICollection<(string, RegistryKey?, Exception?)> list)
        {
            var resultList = new List<Result>();

            foreach (var item in list)
            {
                var result = new Result();

                if (item.Item3 is null && !(item.Item2 is null))
                {
                    // when key contains keys or fields
                    result.Title = item.Item2.Name;
                    result.SubTitle = RegistryHelper.GetSummary(item.Item2);
                }
                else if (item.Item2 is null && !(item.Item3 is null))
                {
                    // on error (e.g access denied)
                    result.Title = item.Item1;
                    result.SubTitle = item.Item3.Message;
                }
                else
                {
                    result.Title = item.Item1;
                }

                result.Action = _ => ContextMenuHelper.OpenInRegistryEditor(result);

                resultList.Add(result);
            }

            return resultList;
        }
    }
}
