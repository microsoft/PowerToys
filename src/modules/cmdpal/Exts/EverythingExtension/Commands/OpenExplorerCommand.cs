// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CmdPal.Extensions.Helpers;

internal sealed partial class OpenExplorerCommand : InvokableCommand
{
    private readonly string _fullname;

    internal OpenExplorerCommand(string fullname)
    {
        _fullname = fullname;
        Name = "Open path";
        Icon = new("\uec50");
    }

    public override CommandResult Invoke()
    {
        var startInfo = new ProcessStartInfo("explorer.exe")
        {
            Arguments = _fullname,  // Set the path to open in File Explorer
            UseShellExecute = true,
        };

        Process.Start(startInfo);

        return CommandResult.KeepOpen();
    }
}
