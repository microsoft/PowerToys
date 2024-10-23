// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CmdPal.Extensions.Helpers;
using Microsoft.UI.Windowing;

namespace EverythingExtension;

internal sealed partial class OpenFileCommand : InvokableCommand
{
    private readonly string _fullname;
    private readonly string _path;

    internal OpenFileCommand(string fullname, string path)
    {
        _fullname = fullname;
        _path = path;
        Name = "Open file";
        Icon = new("\ue8e5");
    }

    public override CommandResult Invoke()
    {
        var startInfo = new ProcessStartInfo(_fullname)
        {
            WorkingDirectory = _path,  // Set the working directory to _path
            UseShellExecute = true,
        };

        Process.Start(startInfo);

        return CommandResult.KeepOpen();
    }
}
