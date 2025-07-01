// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;
using ManagedCsWin32;
using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.CmdPal.Ext.Indexer.Indexer.Utils;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Microsoft.CmdPal.Ext.Indexer.Commands;

internal sealed partial class OpenPropertiesCommand : InvokableCommand
{
    private readonly IndexerItem _item;

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
                FMask = NativeHelpers.SEEMASKINVOKEIDLIST,
            };

            return Shell32.ShellExecuteEx(ref info);
        }
        finally
        {
            Marshal.FreeHGlobal(filenamePtr);
            Marshal.FreeHGlobal(propertiesPtr);
        }
    }

    internal OpenPropertiesCommand(IndexerItem item)
    {
        this._item = item;
        this.Name = Resources.Indexer_Command_OpenProperties;
        this.Icon = new IconInfo("\uE90F");
    }

    public override CommandResult Invoke()
    {
        try
        {
            ShowFileProperties(_item.FullPath);
        }
        catch (Exception ex)
        {
            Logger.LogError("Error showing file properties: ", ex);
        }

        return CommandResult.GoHome();
    }
}
