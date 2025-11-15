// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Text;
using Microsoft.CmdPal.Ext.RemoteDesktop.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.Commands;

internal sealed partial class OpenRemoteDesktopCommand : BaseObservable, IInvokableCommand
{
    private static readonly CompositeFormat ProcessErrorFormat =
        CompositeFormat.Parse(Resources.remotedesktop_log_mstsc_error);

    private static readonly CompositeFormat InvalidHostnameFormat =
        CompositeFormat.Parse(Resources.remotedesktop_log_invalid_hostname);

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
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.WorkingDirectory = Environment.SpecialFolder.MyDocuments.ToString();
        process.StartInfo.FileName = "mstsc";

        if (!string.IsNullOrWhiteSpace(_rdpHost))
        {
            // validate that _rdpHost is a proper hostname or IP address
            if (Uri.CheckHostName(_rdpHost) == UriHostNameType.Unknown)
            {
                return CommandResult.ShowToast(new ToastArgs()
                {
                    Message = string.Format(
                        System.Globalization.CultureInfo.CurrentCulture,
                        InvalidHostnameFormat,
                        _rdpHost),
                    Result = CommandResult.KeepOpen(),
                });
            }

            process.StartInfo.Arguments = $"/v:{_rdpHost}";
        }

        try
        {
            process.Start();

            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast(new ToastArgs()
            {
                Message = string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    ProcessErrorFormat,
                    ex.Message),
                Result = CommandResult.KeepOpen(),
            });
        }
    }
}
