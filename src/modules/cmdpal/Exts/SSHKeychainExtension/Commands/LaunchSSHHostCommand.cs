// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using SSHKeychainExtension.Data;

namespace SSHKeychainExtension.Commands;

internal sealed partial class LaunchSSHHostCommand : InvokableCommand
{
    private readonly SSHKeychainItem _host;

    internal LaunchSSHHostCommand(SSHKeychainItem host)
    {
        this._host = host;
        this.Name = "Connect";
        this.Icon = new IconInfo("\uE8A7");
    }

    public override CommandResult Invoke()
    {
        try
        {
            Process.Start("cmd.exe", $"/k ssh {_host.HostName}");
        }
        catch
        {
            Process.Start(new ProcessStartInfo("cmd.exe", $"/k ssh {_host.HostName}") { UseShellExecute = true });
        }

        return CommandResult.KeepOpen();
    }
}
