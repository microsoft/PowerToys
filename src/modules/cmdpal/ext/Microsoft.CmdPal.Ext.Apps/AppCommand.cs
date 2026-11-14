// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using WyHash;

namespace Microsoft.CmdPal.Ext.Apps;

internal sealed partial class AppCommand : InvokableCommand
{
    private readonly AppItem _app;

    public AppCommand(AppItem app)
    {
        _app = app;
        Name = Resources.run_command_action!;
        Id = GenerateId();
        Icon = Icons.GenericAppIcon;
    }

    private async Task Launch()
    {
        await Task.Run(() =>
        {
            try
            {
                var target = _app.IsPackaged ? "shell:AppsFolder\\" + _app.UserModelId : _app.ExePath;
                ShellHelpers.OpenInShellOrThrow(target);
            }
            catch (System.Exception ex)
            {
                Logger.LogError(ex.Message);
            }
        }).ConfigureAwait(false);
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
