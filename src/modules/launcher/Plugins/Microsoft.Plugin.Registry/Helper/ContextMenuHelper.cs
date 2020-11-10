// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Microsoft.Plugin.Registry.Helper
{
    internal static class ContextMenuHelper
    {
        #pragma warning disable CA1031 // Do not catch general exception types

        internal static bool OpenInRegistryEditor(in Result result)
        {
            try
            {
                RegistryHelper.OpenRegisteryKey(result.Title);
                return true;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                MessageBox.Show(
                    "You have not enought rights to open the Registry Editor",
                    "Error on open Registry Editor",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
            catch (Exception exception)
            {
                Log.Exception("Error on open Registry Editor", exception, typeof(Main));
                return false;
            }
        }

        internal static bool CopyToClipBoard(in Result result)
        {
            try
            {
                Clipboard.Clear();
                Clipboard.SetText(result.Title);
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
