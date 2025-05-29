// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using ManagedCommon;
using Microsoft.CmdPal.Common.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Common.Commands;

public partial class OpenInConsoleCommand : InvokableCommand
{
    internal static IconInfo OpenInConsoleIcon { get; } = new("\uE756");

    private readonly string _path;

    public OpenInConsoleCommand(string fullPath)
    {
        this._path = fullPath;
        this.Name = Resources.Indexer_Command_OpenPathInConsole;
        this.Icon = OpenInConsoleIcon;
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
