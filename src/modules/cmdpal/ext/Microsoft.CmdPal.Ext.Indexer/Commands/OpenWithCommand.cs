// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using ManagedCsWin32;
using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.CmdPal.Ext.Indexer.Indexer.Utils;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Microsoft.CmdPal.Ext.Indexer.Commands;

internal sealed partial class OpenWithCommand : InvokableCommand
{
    private readonly IndexerItem _item;

    private static unsafe bool OpenWith(string filename)
    {
        var filenamePtr = Marshal.StringToHGlobalUni(filename);
        var verbPtr = Marshal.StringToHGlobalUni("openas");

        try
        {
            var info = new Shell32.SHELLEXECUTEINFOW
            {
                CbSize = (uint)sizeof(Shell32.SHELLEXECUTEINFOW),
                LpVerb = verbPtr,
                LpFile = filenamePtr,
                Show = (int)SHOW_WINDOW_CMD.SW_SHOWNORMAL,
                FMask = NativeHelpers.SEEMASKINVOKEIDLIST,
            };

            return Shell32.ShellExecuteEx(ref info);
        }
        finally
        {
            Marshal.FreeHGlobal(filenamePtr);
            Marshal.FreeHGlobal(verbPtr);
        }
    }

    internal OpenWithCommand(IndexerItem item)
    {
        this._item = item;
        this.Name = Resources.Indexer_Command_OpenWith;
        this.Icon = new IconInfo("\uE7AC");
    }

    public override CommandResult Invoke()
    {
        OpenWith(_item.FullPath);

        return CommandResult.GoHome();
    }
}
