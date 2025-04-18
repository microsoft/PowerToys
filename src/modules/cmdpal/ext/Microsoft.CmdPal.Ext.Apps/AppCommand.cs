// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using WyHash;

namespace Microsoft.CmdPal.Ext.Apps;

internal sealed partial class AppCommand : InvokableCommand
{
    private readonly AppItem _app;

    internal AppCommand(AppItem app)
    {
        _app = app;

        Name = Resources.run_command_action;
        Id = GenerateId();
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
        if (_app.IsPackaged)
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

    private string GenerateId()
    {
        // Use WyHash64 to generate stable ID hashes.
        // manually seeding with 0, so that the hash is stable across launches
        var result = WyHash64.ComputeHash64(_app.Name + _app.Subtitle + _app.ExePath, seed: 0);
        return $"{_app.Name}_{result}";
    }
}
