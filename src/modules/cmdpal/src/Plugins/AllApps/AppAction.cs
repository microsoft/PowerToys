// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using AllApps.Programs;
using System.Diagnostics;
using Microsoft.Windows.CommandPalette.Extensions.Helpers;

namespace AllApps;

internal sealed class AppAction : InvokableCommand
{
    private readonly AppItem _app;

    internal AppAction(AppItem app)
    {
        this._Name = "Run";
        this._app = app;
        this._Icon = new(_app.IcoPath);
    }
    internal static async Task StartApp(string amuid)
    {
        var appManager = new ApplicationActivationManager();
        const ActivateOptions noFlags = ActivateOptions.None;
        await Task.Run(() =>
        {
            try
            {
                appManager.ActivateApplication(amuid, /*queryArguments*/ "", noFlags, out var unusedPid);
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
    public override ActionResult Invoke()
    {
        _ = Launch();
        return ActionResult.GoHome();
    }
}
