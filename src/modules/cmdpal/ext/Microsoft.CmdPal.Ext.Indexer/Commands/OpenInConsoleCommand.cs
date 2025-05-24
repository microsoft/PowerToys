// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Indexer.Commands;

internal sealed partial class OpenInConsoleCommand : InvokableCommand
{
    private readonly string _path;

    internal OpenInConsoleCommand(string fullPath)
    {
        this._path = fullPath;
        this.Name = Resources.Indexer_Command_OpenPathInConsole;
        this.Icon = new IconInfo("\uE756");
    }

    public override CommandResult Invoke()
    {
        using (var process = new Process())
        {
            process.StartInfo.WorkingDirectory = Path.GetDirectoryName(_path);
            process.StartInfo.FileName = "cmd.exe";

            try
            {
                process.Start();
            }
            catch (Win32Exception ex)
            {
                Logger.LogError($"Unable to open '{_path}'", ex);
            }
        }

        return CommandResult.Dismiss();
    }
}
