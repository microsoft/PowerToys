// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps.Commands;

internal sealed partial class OpenPathCommand : InvokableCommand
{
    private static readonly IconInfo TheIcon = new("\ue838");

    private readonly string _target;

    public OpenPathCommand(string target)
    {
        Name = Resources.open_location;
        Icon = TheIcon;

        _target = target;
    }

    internal static async Task LaunchTarget(string t)
    {
        await Task.Run(() =>
        {
            Process.Start(new ProcessStartInfo(t) { UseShellExecute = true });
        });
    }

    public override CommandResult Invoke()
    {
        _ = LaunchTarget(_target).ConfigureAwait(false);

        return CommandResult.Dismiss();
    }
}
