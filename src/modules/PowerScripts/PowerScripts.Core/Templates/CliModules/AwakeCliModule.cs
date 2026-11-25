// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using static PowerScripts.Core.Templates.CliRecipeBuilder;

namespace PowerScripts.Core.Templates.CliModules;

internal sealed class AwakeCliModule : PowerShellCliModule
{
    public override string Id => "awake";

    public override string Name => "Awake";

    public override string IconName => "Awake";

    public override string Executable => @"PowerToys.Awake.exe";

    public override int Order => 60;

    public override IReadOnlyList<PowerShellCliTemplate> CreateRecipes() =>
    [
        Describe(AsAction(Template("awake", "Awake: keep the machine awake",
            "Starts a keep-awake process for a limited duration by default. An already-running Awake instance prevents another CLI instance from starting. This is a long-running CLI; increase the PowerScripts execution timeout deliberately if needed.", "none",
            """
            if ($UseConfiguration) { $__powerscriptArguments.Add('--use-pt-config') }
            else {
                if ($ExpireAt) { $__powerscriptArguments.AddRange([string[]]@('--expire-at', $ExpireAt)) }
                else { $__powerscriptArguments.AddRange([string[]]@('--time-limit', $TimeLimitSeconds.ToString([Globalization.CultureInfo]::InvariantCulture))) }
                if ($DisplayOn) { $__powerscriptArguments.Add('--display-on') }
                if ($ParentProcess) {
                    if ($ProcessId -gt 0) { throw 'Choose ParentProcess or ProcessId, not both.' }
                    $__powerscriptArguments.Add('--use-parent-pid')
                } elseif ($ProcessId -gt 0) { $__powerscriptArguments.AddRange([string[]]@('--pid', $ProcessId.ToString([Globalization.CultureInfo]::InvariantCulture))) }
            }
            """,
            Integer("TimeLimitSeconds", "Seconds to remain awake; zero means indefinite and is not the default.", 10),
            Flag("DisplayOn", "Keep the display on too."),
            Text("ExpireAt", "Optional CLI date/time string instead of TimeLimitSeconds."),
            Integer("ProcessId", "Optional process whose lifetime bounds Awake; zero omits it.", 0),
            Flag("ParentProcess", "Bind Awake to its parent script process."),
            Flag("UseConfiguration", "Use existing PowerToys Awake settings instead of the other options.")) with { Id = "awake-action" }, writesState: false),
            "--time-limit | --expire-at; --display-on; --pid | --use-parent-pid; --use-pt-config"),
    ];
}
