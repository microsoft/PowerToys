// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.Bookmarks;

internal sealed class OpenInTerminalAction : InvokableCommand
{
    private readonly string _folder;

    public OpenInTerminalAction(string folder)
    {
        Name = "Open in Terminal";
        _folder = folder;
    }

    public override ICommandResult Invoke()
    {
        try
        {
            // Start Windows Terminal with the specified folder
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "wt.exe",
                Arguments = $"-d \"{_folder}\"",
                UseShellExecute = true,
            };
            System.Diagnostics.Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error launching Windows Terminal: {ex.Message}");
        }

        return CommandResult.Dismiss();
    }
}
