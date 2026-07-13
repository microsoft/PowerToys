// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PowerScripts;

/// <summary>
/// Invokes a single PowerScript by id. Execution is offloaded to a background task so the palette UI
/// thread is never blocked; the host takes over from there (including the optional parameter prompt).
/// </summary>
internal sealed partial class RunPowerScriptCommand : InvokableCommand
{
    private readonly string _scriptId;

    public RunPowerScriptCommand(string scriptId, string name, IconInfo icon)
    {
        _scriptId = scriptId;
        Name = name;
        Icon = icon;
        Id = $"com.microsoft.cmdpal.builtin.powerscripts.run.{scriptId}";
    }

    public override ICommandResult Invoke()
    {
        var id = _scriptId;
        Task.Run(() => PowerScriptHostClient.Run(id));
        return CommandResult.Dismiss();
    }
}
