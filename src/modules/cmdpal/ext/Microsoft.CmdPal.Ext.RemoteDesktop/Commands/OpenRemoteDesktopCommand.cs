// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CmdPal.Ext.RemoteDesktop.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.Commands;

internal sealed partial class OpenRemoteDesktopCommand : BaseObservable, IInvokableCommand
{
    public string Name { get; }

    public string Id { get; } = "com.microsoft.cmdpal.builtin.remotedesktop.openrdp";

    public IIconInfo Icon => Icons.RDPIcon;

    private readonly string _rdpHost;

    public OpenRemoteDesktopCommand(string rdpHost)
    {
        _rdpHost = rdpHost;

        Name = string.IsNullOrWhiteSpace(_rdpHost) ?
                    Resources.remotedesktop_command_open :
                    Resources.remotedesktop_command_connect;
    }

    public ICommandResult Invoke(object sender)
    {
        using var process = new Process();
        process.StartInfo.FileName = "mstsc";
        if (!string.IsNullOrWhiteSpace(_rdpHost))
        {
            process.StartInfo.Arguments = $"/v:{_rdpHost}";
        }

        process.Start();
        return CommandResult.Dismiss();
    }
}
