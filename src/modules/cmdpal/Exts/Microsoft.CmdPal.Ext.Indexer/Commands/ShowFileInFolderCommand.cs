// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.Indexer.Commands;

internal sealed partial class ShowFileInFolderCommand : InvokableCommand
{
    private readonly IndexerItem _item;

    internal ShowFileInFolderCommand(IndexerItem item)
    {
        this._item = item;
        this.Name = Resources.Indexer_Command_ShowInFolder;
        this.Icon = new("\uE838");
    }

    public override CommandResult Invoke()
    {
        if (File.Exists(_item.FullPath))
        {
            try
            {
                var argument = "/select, \"" + _item.FullPath + "\"";
                Process.Start("explorer.exe", argument);
            }
            catch (Exception ex)
            {
                Logger.LogError("Invoke exception: ", ex);
            }
        }

        return CommandResult.GoHome();
    }
}
