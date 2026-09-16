// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PowerToys.TryRun.CmdPal;

public sealed partial class TryRunCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] commands;

    public TryRunCommandsProvider()
    {
        Id = "com.microsoft.powertoys.tryrun.development";
        DisplayName = "Try Run (experimental)";
        Icon = new IconInfo("\uE768");
        var application = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "PowerToys.TryRun.exe"));
        commands =
        [
            new CommandItem(new OpenTryRunCommand(application, [])) { Title = "Try Run", Subtitle = "Configure a Windows application or script in MXC" },
            new CommandItem(new TryRunFilePage(application)) { Title = "Try Run a file", Subtitle = "Paste a file or folder path to configure its run" },
        ];
    }

    public override ICommandItem[] TopLevelCommands() => commands;
}
