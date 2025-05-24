// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Indexer.Commands;

internal sealed partial class CopyPathCommand : InvokableCommand
{
    private readonly string _path;

    internal CopyPathCommand(string fullPath)
    {
        this._path = fullPath;
        this.Name = Resources.Indexer_Command_CopyPath;
        this.Icon = new IconInfo("\uE8c8");
    }

    public override CommandResult Invoke()
    {
        try
        {
            ClipboardHelper.SetText(_path);
        }
        catch
        {
        }

        return CommandResult.KeepOpen();
    }
}
