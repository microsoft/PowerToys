// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.CmdPal.Ext.Indexer.Native;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Microsoft.CmdPal.Ext.Indexer.Commands;

internal sealed partial class OpenPropertiesCommand : InvokableCommand
{
    private readonly IndexerItem _item;

    private static unsafe bool ShowFileProperties(string filename)
    {
        var info = new NativeMethods.SHELLEXECUTEINFOW
        {
            cbSize = Unsafe.SizeOf<NativeMethods.SHELLEXECUTEINFOW>(),
            lpVerb = "properties",
            lpFile = filename,

            nShow = (int)NativeMethods.SHOW_WINDOW_CMD.SW_SHOW,
            fMask = NativeHelpers.SEEMASKINVOKEIDLIST,
        };

        return NativeMethods.ShellExecuteEx(ref info);
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
