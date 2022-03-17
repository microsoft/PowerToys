// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Microsoft.PowerToys.Run.Plugin.System.Properties;
using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.System.Components
{
    internal static class ResultHelper
    {
        internal static bool ExecuteCommand(bool confirm, string confirmationMessage, Action command)
        {
            if (confirm)
            {
                MessageBoxResult messageBoxResult = MessageBox.Show(
                    confirmationMessage,
                    Resources.Microsoft_plugin_sys_confirmation,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (messageBoxResult == MessageBoxResult.No)
                {
                    return false;
                }
            }

            command();
            return true;
        }

        internal static bool CopyToClipBoard(in string text)
        {
            try
            {
                Clipboard.Clear();
                Clipboard.SetText(text);
                return true;
            }
            catch (Exception exception)
            {
                Log.Exception("Can't copy to clipboard", exception, typeof(ResultHelper));
                return false;
            }
        }

        internal static List<ContextMenuResult> GetContextMenuForResult(Result result)
        {
            var contextMenu = new List<ContextMenuResult>();

            if (!(result?.ContextData is SystemPluginContext contextData))
            {
                return contextMenu;
            }

            if (contextData.Type == ResultContextType.NetworkAdapterInfo)
            {
                contextMenu.Add(new ContextMenuResult()
                {
                    AcceleratorKey = Key.C,
                    AcceleratorModifiers = ModifierKeys.Control,
                    FontFamily = "Segoe MDL2 Assets",
                    Glyph = "\xE8C8",                       // E8C8 => Symbol: Copy
                    Title = Resources.Microsoft_plugin_sys_CopyDetails,
                    Action = _ => ResultHelper.CopyToClipBoard(contextData.Data),
                });
            }

            return contextMenu;
        }
    }
}
