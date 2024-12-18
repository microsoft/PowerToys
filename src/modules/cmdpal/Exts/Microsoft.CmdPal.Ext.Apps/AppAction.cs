// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

internal sealed partial class AppAction : InvokableCommand
{
    private readonly AppItem _app;

    internal AppAction(AppItem app)
    {
        _app = app;

        Name = "Run";
        Icon = new(_app.IcoPath);
    }

    internal static async Task StartApp(string aumid)
    {
        var appManager = new ApplicationActivationManager();
        const ActivateOptions noFlags = ActivateOptions.None;
        await Task.Run(() =>
        {
            try
            {
                appManager.ActivateApplication(aumid, /*queryArguments*/ string.Empty, noFlags, out var unusedPid);
            }
            catch (System.Exception)
            {
            }
        }).ConfigureAwait(false);
    }

    internal static async Task StartExe(string path)
    {
        var appManager = new ApplicationActivationManager();

        // const ActivateOptions noFlags = ActivateOptions.None;
        await Task.Run(() =>
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        });
    }

    internal async Task Launch()
    {
        if (string.IsNullOrEmpty(_app.ExePath))
        {
            await StartApp(_app.UserModelId);
        }
        else
        {
            await StartExe(_app.ExePath);
        }
    }

    public override CommandResult Invoke()
    {
        _ = Launch();
        return CommandResult.Dismiss();
    }
}
