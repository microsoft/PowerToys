// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Indexer.Commands;

internal sealed partial class OpenFileCommand : InvokableCommand
{
    private readonly IndexerItem _item;

    internal OpenFileCommand(IndexerItem item)
    {
        this._item = item;
        this.Name = Resources.Indexer_Command_OpenFile;
        this.Icon = Icons.OpenFile;
    }

    public override CommandResult Invoke()
    {
        using (var process = new Process())
        {
            process.StartInfo.FileName = _item.FullPath;
            process.StartInfo.UseShellExecute = true;

            try
            {
                process.Start();
            }
            catch (Win32Exception ex)
            {
                Logger.LogError($"Unable to open {_item.FullPath}", ex);
            }
        }

        return CommandResult.GoHome();
    }
}
