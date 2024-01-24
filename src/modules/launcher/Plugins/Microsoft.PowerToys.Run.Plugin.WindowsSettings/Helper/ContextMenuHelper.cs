// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Microsoft.PowerToys.Run.Plugin.WindowsSettings.Properties;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.WindowsSettings.Helper
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
        internal static List<ContextMenuResult> GetContextMenu(in Result result, in string assemblyName)
        {
            if (!(result?.ContextData is WindowsSetting entry))
            {
                return new List<ContextMenuResult>(0);
            }

            var list = new List<ContextMenuResult>(1)
            {
                new ContextMenuResult
                {
                    AcceleratorKey = Key.C,
                    AcceleratorModifiers = ModifierKeys.Control,
                    Action = _ => TryToCopyToClipBoard(entry.Command),
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    Glyph = "\xE8C8",                       // E8C8 => Symbol: Copy
                    PluginName = assemblyName,
                    Title = $"{Resources.CopyCommand} (Ctrl+C)",
                },
            };

            return list;
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
