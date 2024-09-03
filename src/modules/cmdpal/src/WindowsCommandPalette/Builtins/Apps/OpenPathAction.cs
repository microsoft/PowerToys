// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Windows.CommandPalette.Extensions.Helpers;

namespace WindowsCommandPalette.BuiltinCommands.AllApps;

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
