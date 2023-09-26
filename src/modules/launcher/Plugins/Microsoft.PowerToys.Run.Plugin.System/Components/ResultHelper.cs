// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.PowerToys.Run.Plugin.System.Properties;
using Wox.Plugin;
using Wox.Plugin.Common.Win32;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.System.Components
{
    internal static class ResultHelper
    {
        private static bool executingEmptyRecycleBinTask;

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

        internal static async void EmptyRecycleBinAsync(bool settingEmptyRBSuccesMsg)
        {
            if (executingEmptyRecycleBinTask)
            {
                _ = MessageBox.Show(Resources.Microsoft_plugin_sys_RecycleBin_EmptyTaskRunning, "Plugin: " + Resources.Microsoft_plugin_sys_plugin_name, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await Task.Run(() => EmptyRecycleBinTask(settingEmptyRBSuccesMsg));
        }

        internal static List<ContextMenuResult> GetContextMenuForResult(Result result, bool settingEmptyRBSuccesMsg)
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
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    Glyph = "\xE8C8",                       // E8C8 => Symbol: Copy
                    Title = Resources.Microsoft_plugin_sys_CopyDetails,
                    Action = _ => CopyToClipBoard(contextData.Data),
                });
            }

            if (contextData.Type == ResultContextType.RecycleBinCommand)
            {
                contextMenu.Add(new ContextMenuResult()
                {
                    AcceleratorKey = Key.Delete,
                    AcceleratorModifiers = ModifierKeys.Shift, // Shift+Delete is the common key for deleting without recycle bin
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    Glyph = "\xE74D",                       // E74D => Symbol: Delete
                    Title = Resources.Microsoft_plugin_sys_RecycleBin_contextMenu,
                    Action = _ =>
                    {
                        EmptyRecycleBinAsync(settingEmptyRBSuccesMsg);
                        return true;
                    },
                });
            }

            return contextMenu;
        }

        /// <summary>
        /// Method to process the empty recycle bin command in a separate task
        /// </summary>
        private static void EmptyRecycleBinTask(bool settingEmptyRBSuccesMsg)
        {
            executingEmptyRecycleBinTask = true;

            // https://learn.microsoft.com/windows/win32/api/shellapi/nf-shellapi-shemptyrecyclebina/
            // http://www.pinvoke.net/default.aspx/shell32/SHEmptyRecycleBin.html/
            // If the recycle bin is already empty, it will return -2147418113 (0x8000FFFF (E_UNEXPECTED))
            // If the user canceled the deletion task it will return 2147943623 (0x800704C7 (E_CANCELLED - The operation was canceled by the user.))
            // On success it will return 0 (S_OK)
            var result = NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, 0);
            if (result == (uint)HRESULT.E_UNEXPECTED)
            {
                _ = MessageBox.Show(Resources.Microsoft_plugin_sys_RecycleBin_IsEmpty, "Plugin: " + Resources.Microsoft_plugin_sys_plugin_name, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (result != (uint)HRESULT.S_OK && result != (uint)HRESULT.E_CANCELLED)
            {
                var errorDesc = Win32Helpers.MessageFromHResult((int)result);
                var name = "Plugin: " + Resources.Microsoft_plugin_sys_plugin_name;
                var message = $"{Resources.Microsoft_plugin_sys_RecycleBin_ErrorMsg} {errorDesc}";
                Log.Error(message + " - Please refer to https://msdn.microsoft.com/library/windows/desktop/aa378137 for more information.", typeof(Commands));
                _ = MessageBox.Show(message, name, MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (result == (uint)HRESULT.S_OK && settingEmptyRBSuccesMsg)
            {
                _ = MessageBox.Show(Resources.Microsoft_plugin_sys_RecycleBin_EmptySuccessMessage, "Plugin: " + Resources.Microsoft_plugin_sys_plugin_name, MessageBoxButton.OK, MessageBoxImage.Information);
            }

            executingEmptyRecycleBinTask = false;
        }
    }
}
