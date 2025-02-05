// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.CmdPal.Ext.Indexer.Native;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
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
            var filenamePCWSTR = new PCWSTR((char*)filenamePtr);
            var verbPCWSTR = new PCWSTR((char*)verbPtr);

            var info = new SHELLEXECUTEINFOW
            {
                cbSize = (uint)Marshal.SizeOf<SHELLEXECUTEINFOW>(),
                lpVerb = verbPCWSTR,
                lpFile = filenamePCWSTR,
                nShow = (int)SHOW_WINDOW_CMD.SW_SHOWNORMAL,
                fMask = NativeHelpers.SEEMASKINVOKEIDLIST,
            };

            return PInvoke.ShellExecuteEx(ref info);
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
