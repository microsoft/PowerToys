// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.Bookmarks.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Bookmarks;

internal sealed partial class OpenInTerminalCommand : InvokableCommand
{
    private readonly string _folder;

    public OpenInTerminalCommand(string folder)
    {
        Name = Resources.bookmarks_open_in_terminal_name;
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
