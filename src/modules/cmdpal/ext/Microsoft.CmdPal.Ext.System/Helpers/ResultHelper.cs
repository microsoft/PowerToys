// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;
using static Microsoft.CmdPal.Ext.System.Helpers.MessageBoxHelper;

namespace Microsoft.CmdPal.Ext.System.Helpers;

internal static class ResultHelper
{
    public static bool ExecutingEmptyRecycleBinTask { get; set; }

    /// <summary>
    /// Method to process the empty recycle bin command in a separate task
    /// </summary>
    public static void EmptyRecycleBinTask(bool settingEmptyRBSuccesMsg)
    {
        ExecutingEmptyRecycleBinTask = true;

        // https://learn.microsoft.com/windows/win32/api/shellapi/nf-shellapi-shemptyrecyclebina/
        // http://www.pinvoke.net/default.aspx/shell32/SHEmptyRecycleBin.html/
        // If the recycle bin is already empty, it will return -2147418113 (0x8000FFFF (E_UNEXPECTED))
        // If the user canceled the deletion task it will return 2147943623 (0x800704C7 (E_CANCELLED - The operation was canceled by the user.))
        // On success it will return 0 (S_OK)
        var result = NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, 0);
        if (result == (uint)HRESULT.E_UNEXPECTED)
        {
            _ = MessageBoxHelper.Show(Resources.Microsoft_plugin_sys_RecycleBin_IsEmpty, "Plugin: " + Resources.Microsoft_plugin_sys_plugin_name, IconType.Info, MessageBoxType.OK);
        }
        else if (result != (uint)HRESULT.S_OK && result != (uint)HRESULT.E_CANCELLED)
        {
            var errorDesc = Win32Helpers.MessageFromHResult((int)result);
            var name = "Plugin: " + Resources.Microsoft_plugin_sys_plugin_name;
            var message = $"{Resources.Microsoft_plugin_sys_RecycleBin_ErrorMsg} {errorDesc}";

            ExtensionHost.LogMessage(new LogMessage() { Message = message + " - Please refer to https://msdn.microsoft.com/library/windows/desktop/aa378137 for more information." });

            _ = MessageBoxHelper.Show(message, name, IconType.Error, MessageBoxType.OK);
        }

        if (result == (uint)HRESULT.S_OK && settingEmptyRBSuccesMsg)
        {
            _ = MessageBoxHelper.Show(Resources.Microsoft_plugin_sys_RecycleBin_EmptySuccessMessage, "Plugin: " + Resources.Microsoft_plugin_sys_plugin_name, IconType.Info, MessageBoxType.OK);
        }

        ExecutingEmptyRecycleBinTask = false;
    }
}
