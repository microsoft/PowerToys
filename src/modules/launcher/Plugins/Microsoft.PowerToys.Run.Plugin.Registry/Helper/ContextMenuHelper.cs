// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Microsoft.PowerToys.Run.Plugin.Registry.Classes;
using Microsoft.PowerToys.Run.Plugin.Registry.Properties;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.Registry.Helper
{
    /// <summary>
    /// Helper class to easier work with context menu entries
    /// </summary>
    internal static class ContextMenuHelper
    {
        /// <summary>
        /// Return a list with all context menu entries for the given <see cref="Result"/>
        /// <para>Symbols taken from <see href="https://learn.microsoft.com/windows/uwp/design/style/segoe-ui-symbol-font"/></para>
        /// </summary>
        /// <param name="result">The result for the context menu entries</param>
        /// <param name="assemblyName">The name of the this assembly</param>
        /// <returns>A list with context menu entries</returns>
        internal static List<ContextMenuResult> GetContextMenu(Result result, string assemblyName)
        {
            if (!(result?.ContextData is RegistryEntry entry))
            {
                return new List<ContextMenuResult>(0);
            }

            var list = new List<ContextMenuResult>();

            if (string.IsNullOrEmpty(entry.ValueName))
            {
                list.Add(new ContextMenuResult
                {
                    AcceleratorKey = Key.C,
                    AcceleratorModifiers = ModifierKeys.Control,
                    Action = _ => TryToCopyToClipBoard(entry.GetRegistryKey()),
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    Glyph = "\xE8C8",                       // E8C8 => Symbol: Copy
                    PluginName = assemblyName,
                    Title = $"{Resources.CopyKeyNamePath} (Ctrl+C)",
                });
            }
            else
            {
                list.Add(new ContextMenuResult
                {
                    AcceleratorKey = Key.C,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Action = _ => TryToCopyToClipBoard(entry.GetValueData()),
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    Glyph = "\xF413",                       // F413 => Symbol: CopyTo
                    PluginName = assemblyName,
                    Title = $"{Resources.CopyValueData} (Ctrl+Shift+C)",
                });

                list.Add(new ContextMenuResult
                {
                    AcceleratorKey = Key.C,
                    AcceleratorModifiers = ModifierKeys.Control,
                    Action = _ => TryToCopyToClipBoard(entry.GetValueNameWithKey()),
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    Glyph = "\xE8C8",                       // E8C8 => Symbol: Copy
                    PluginName = assemblyName,
                    Title = $"{Resources.CopyValueName} (Ctrl+C)",
                });
            }

            list.Add(new ContextMenuResult
            {
                AcceleratorKey = Key.Enter,
                AcceleratorModifiers = ModifierKeys.Control,
                Action = _ => TryToOpenInRegistryEditor(entry),
                FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                Glyph = "\xE8A7",                           // E8A7 => Symbol: OpenInNewWindow
                PluginName = assemblyName,
                Title = $"{Resources.OpenKeyInRegistryEditor} (Ctrl+Enter)",
            });

            return list;
        }

        /// <summary>
        /// Open the Windows registry editor and jump to registry key inside the given key (inside the <see cref="RegistryEntry"/>
        /// </summary>
        /// <param name="entry">The <see cref="RegistryEntry"/> to jump in</param>
        /// <returns><see langword="true"/> if the registry editor was successful open, otherwise <see langword="false"/></returns>
        internal static bool TryToOpenInRegistryEditor(in RegistryEntry entry)
        {
            try
            {
                RegistryHelper.OpenRegistryKey(entry.Key?.Name ?? entry.KeyPath);
                return true;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                MessageBox.Show(
                    Resources.OpenInRegistryEditorAccessExceptionText,
                    Resources.OpenInRegistryEditorAccessExceptionTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
            catch (Exception exception)
            {
                Log.Exception("Error on opening Windows registry editor", exception, typeof(Main));
                return false;
            }
        }

        /// <summary>
        /// Copy the given text to the clipboard
        /// </summary>
        /// <param name="text">The text to copy to the clipboard</param>
        /// <returns><see langword="true"/>The text successful copy to the clipboard, otherwise <see langword="false"/></returns>
        private static bool TryToCopyToClipBoard(in string text)
        {
            try
            {
                Clipboard.Clear();
                Clipboard.SetText(text);
                return true;
            }
            catch (Exception exception)
            {
                Log.Exception("Can't copy to clipboard", exception, typeof(Main));
                return false;
            }
        }
    }
}
