// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using Microsoft.Plugin.Registry.Classes;
using Wox.Plugin.Logger;

namespace Microsoft.Plugin.Registry.Helper
{
    /// <summary>
    /// Helper class to easier work with context menu entries
    /// </summary>
    internal static class ContextMenuHelper
    {
        #pragma warning disable CA1031 // Do not catch general exception types

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
                    "You do not have enough rights to open the Windows registry editor",
                    "Error on open Registry Editor",
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
        internal static bool TryToCopyToClipBoard(in string text)
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
