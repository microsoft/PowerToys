// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;
using ManagedCsWin32;
using Microsoft.CmdPal.Common.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Microsoft.CmdPal.Common.Commands;

public partial class OpenPropertiesCommand : InvokableCommand
{
    internal static IconInfo OpenPropertiesIcon { get; } = new("\uE90F");

    private readonly string _path;

    private static unsafe bool ShowFileProperties(string filename)
    {
        var propertiesPtr = Marshal.StringToHGlobalUni("properties");
        var filenamePtr = Marshal.StringToHGlobalUni(filename);

        try
        {
            var info = new Shell32.SHELLEXECUTEINFOW
            {
                CbSize = (uint)sizeof(Shell32.SHELLEXECUTEINFOW),
                LpVerb = propertiesPtr,
                LpFile = filenamePtr,
                Show = (int)SHOW_WINDOW_CMD.SW_SHOW,
                FMask = global::Windows.Win32.PInvoke.SEE_MASK_INVOKEIDLIST,
            };

            return Shell32.ShellExecuteEx(ref info);
        }
        finally
        {
            Marshal.FreeHGlobal(filenamePtr);
            Marshal.FreeHGlobal(propertiesPtr);
        }
    }

    public OpenPropertiesCommand(string fullPath)
    {
        this._path = fullPath;
        this.Name = Resources.Indexer_Command_OpenProperties;
        this.Icon = OpenPropertiesIcon;
    }

    public override CommandResult Invoke()
    {
        try
        {
            ShowFileProperties(_path);
        }
        catch (Exception ex)
        {
            Logger.LogError("Error showing file properties: ", ex);
        }

        return CommandResult.Dismiss();
    }
}
