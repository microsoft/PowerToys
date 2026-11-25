// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using static PowerScripts.Core.Templates.CliRecipeBuilder;

namespace PowerScripts.Core.Templates.CliModules;

internal sealed class LightSwitchCliModule : PowerShellCliModule
{
    private const string Arguments = """
            switch ($Command) {
                'schedule-enable' { $__powerscriptArguments.AddRange([string[]]@('schedule', 'enable', '--mode', $ScheduleMode)) }
                'schedule-disable' { $__powerscriptArguments.AddRange([string[]]@('schedule', 'disable')) }
                default { $__powerscriptArguments.Add($Command) }
            }
            if ($Json) { $__powerscriptArguments.Add('--json') }
            """;

    public override string Id => "light-switch";

    public override string Name => "Light Switch";

    public override string IconName => "LightSwitch";

    public override string Executable => @"PowerToys.LightSwitch.Cli.exe";

    public override int Order => 70;

    public override IReadOnlyList<PowerShellCliTemplate> CreateRecipes() =>
    [
        Describe(Variant(Arguments, "light-switch-status", "Light Switch: query theme and schedule",
            "Read the current theme/schedule from the running host module.", "text", "$Command = 'status'",
            Flag("Json", "Return structured JSON text.", true)),
            "status [--json]"),
        Describe(Variant(Arguments, "light-switch-theme", "Light Switch: change the theme",
            "Select light/dark or toggle the host desktop theme.", "none", string.Empty,
            Text("Command", "Theme action.", "toggle", "light", "dark", "toggle")),
            "light | dark | toggle"),
        Describe(Variant(Arguments, "light-switch-schedule", "Light Switch: enable or disable scheduling",
            "Change saved theme scheduling. Fixed hours/location remain configured in module Settings.", "none", string.Empty,
            Text("Command", "Schedule action.", "schedule-enable", "schedule-enable", "schedule-disable"),
            Text("ScheduleMode", "Used by schedule-enable.", "fixed-hours", "fixed-hours", "sunset-to-sunrise", "follow-night-light")),
            "schedule enable --mode fixed-hours|sunset-to-sunrise|follow-night-light | schedule disable"),
    ];
}
