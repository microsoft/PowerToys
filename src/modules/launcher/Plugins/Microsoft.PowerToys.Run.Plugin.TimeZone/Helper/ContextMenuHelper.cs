// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Microsoft.PowerToys.Run.Plugin.TimeZone.Classes;
using Microsoft.PowerToys.Run.Plugin.TimeZone.Properties;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.TimeZone.Helper
{
    /// <summary>
    /// Helper class to easier work with context menu entries
    /// </summary>
    internal static class ContextMenuHelper
    {
        /// <summary>
        /// Return a list with all context menu entries for the given <see cref="Result"/>
        /// <para>Symbols taken from <see href="https://docs.microsoft.com/en-us/windows/uwp/design/style/segoe-ui-symbol-font"/></para>
        /// </summary>
        /// <param name="result">The result for the context menu entires</param>
        /// <param name="assemblyName">The name of the this assembly</param>
        /// <returns>A list with context menu entries</returns>
        internal static List<ContextMenuResult> GetContextMenu(in Result result, in string assemblyName)
        {
            if (!(result?.ContextData is DateTime dateTime))
            {
                return new List<ContextMenuResult>(0);
            }

            var list = new List<ContextMenuResult>
            {
                new ContextMenuResult
                {
                    AcceleratorKey = Key.C,
                    AcceleratorModifiers = ModifierKeys.Control,
                    Action = _ => TryToCopyToClipBoard($"{dateTime:HH:mm:ss}"),
                    FontFamily = "Segoe MDL2 Assets",
                    Glyph = "\xE8C8",                       // E8C8 => Symbol: Copy
                    PluginName = assemblyName,
                    Title = $"{Resources.CopyTime} (Ctrl+C)",
                },
            };

            return list;
        }

#pragma warning disable CA1031 // Do not catch general exception types

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

        #pragma warning restore CA1031 // Do not catch general exception types
    }
}
