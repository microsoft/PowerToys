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

internal sealed partial class OpenRemoteDesktopCommand : BaseObservable, IInvokableCommand2
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
        => InvokeCore(_rdpHost);

    public ICommandResult InvokeWithArgs(object sender, IFallbackCommandInvocationArgs args)
        => InvokeCore(args.Query);

    internal static bool TryGetValidatedHost(string host, out string validatedHost)
    {
        validatedHost = host.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            return true;
        }

        // Strip port suffix (e.g. "localhost:3389") before validation,
        // since Uri.CheckHostName does not accept host:port strings.
        var hostForValidation = validatedHost;
        var lastColon = validatedHost.LastIndexOf(':');
        if (lastColon > 0 && lastColon < validatedHost.Length - 1)
        {
            var portPart = validatedHost[(lastColon + 1)..];
            if (ushort.TryParse(portPart, out _))
            {
                hostForValidation = validatedHost[..lastColon];
            }
        }

        return Uri.CheckHostName(hostForValidation) != UriHostNameType.Unknown;
    }

    private ICommandResult InvokeCore(string host)
    {
        using var process = new Process();
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        process.StartInfo.FileName = "mstsc";

        if (!TryGetValidatedHost(host, out var validatedHost))
        {
            return CommandResult.ShowToast(new ToastArgs()
            {
                Message = string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    InvalidHostnameFormat,
                    host),
                Result = CommandResult.KeepOpen(),
            });
        }

        if (!string.IsNullOrWhiteSpace(validatedHost))
        {
            process.StartInfo.Arguments = $"/v:{validatedHost}";
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
