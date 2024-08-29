// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Windows.CommandPalette.Extensions.Helpers;

namespace AllApps;

// NOTE this is pretty close to what we'd put in the SDK
internal sealed class OpenPathAction(string target) : InvokableCommand
{
    private readonly string _Target = target;
    internal static async Task LaunchTarget(string t)
    {
        await Task.Run(() =>
        {
            Process.Start(new ProcessStartInfo(t) { UseShellExecute = true });
        });
    }
    public override ActionResult Invoke()
    {
        LaunchTarget(this._Target).Start();
        return ActionResult.GoHome();
    }
}