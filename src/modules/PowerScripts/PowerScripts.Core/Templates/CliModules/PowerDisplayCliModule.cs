// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using static PowerScripts.Core.Templates.CliRecipeBuilder;

namespace PowerScripts.Core.Templates.CliModules;

internal sealed class PowerDisplayCliModule : PowerShellCliModule
{
    private const string PowerDisplayProfileScript = """
            $__powerscriptArguments.AddRange([string[]]@('apply-profile', $ProfileId.ToString([Globalization.CultureInfo]::InvariantCulture)))
            """;

    private const string PowerDisplayAdjustActionScript = """

            $__powerscriptArguments.Add('--' + $Setting)
            if ($Command -eq 'set') { $__powerscriptArguments.Add($Value.ToString([Globalization.CultureInfo]::InvariantCulture)) }
            else { $__powerscriptArguments.AddRange([string[]]@('--step', $Step.ToString([Globalization.CultureInfo]::InvariantCulture))) }
            """;

    private const string PowerDisplayDiscreteActionScript = """

            if ($Setting -eq 'orientation') {
                if ($Value -notin @('0', '90', '180', '270')) { throw 'Orientation must be 0, 90, 180 or 270.' }
            } elseif ($Value -notmatch '^(0[xX])?[0-9a-fA-F]{1,4}$') { throw 'Choose a supported hexadecimal VCP value.' }
            $__powerscriptArguments.AddRange([string[]]@(('--' + $Setting), $Value))
            """;

    private const string DisplayTargets = """
        if ($MonitorId) { $__powerscriptArguments.AddRange([string[]]@('--monitor-id', $MonitorId)) }
        elseif ($MonitorNumbers.Trim()) {
            $__powerscriptNumbers = @(
                foreach ($__powerscriptPart in ($MonitorNumbers -split ',')) {
                    $__powerscriptNumber = 0
                    if (-not [int]::TryParse($__powerscriptPart.Trim(), [Globalization.NumberStyles]::Integer, [Globalization.CultureInfo]::InvariantCulture, [ref]$__powerscriptNumber) -or
                        $__powerscriptNumber -lt 1) { throw 'MonitorNumbers requires comma-separated positive monitor indices.' }
                    $__powerscriptNumber.ToString([Globalization.CultureInfo]::InvariantCulture)
                }
            )
            $__powerscriptArguments.AddRange([string[]]@('--monitor-number', ($__powerscriptNumbers -join ',')))
        }
        """;

    private const string QueryArguments = """
            $__powerscriptArguments.Add($Command)
            if ($Command -in @('get', 'capabilities')) {
                if ($MonitorId) { $__powerscriptArguments.AddRange([string[]]@('--monitor-id', $MonitorId)) }
                elseif ($MonitorNumber -gt 0) { $__powerscriptArguments.AddRange([string[]]@('--monitor-number', $MonitorNumber.ToString([Globalization.CultureInfo]::InvariantCulture))) }
                if ($Setting) { $__powerscriptArguments.AddRange([string[]]@('--setting', $Setting)) }
            }
            """;

    public override string Id => "power-display";

    public override string Name => "Power Display";

    public override string IconName => "PowerDisplay";

    public override string Executable => @"WinUI3Apps\PowerToys.PowerDisplay.Cli.exe";

    public override int Order => 80;

    public override IReadOnlyList<PowerShellCliTemplate> CreateRecipes() =>
    [
        Describe(
            Variant(
                QueryArguments,
                "power-display-inspect",
                "Power Display: query monitors, capabilities or profiles",
                "Read connected monitors, setting values, capabilities or saved profiles. Get/capabilities target at most one monitor.",
                "text",
                string.Empty,
                [Text("Command", "Read-only query.", "list", "list", "capabilities", "get", "profiles"),  Text("MonitorId", "Optional monitor ID; takes precedence over MonitorNumber."),  Integer("MonitorNumber", "One-based monitor number; zero lets the CLI select displays.", 0),  Text("Setting", "Optional setting filter.", string.Empty, string.Empty, "brightness", "contrast", "volume", "color-temperature", "input-source", "power-state", "orientation"), Flag("Json", "Return structured JSON Lines text.", true)])
            with { Arguments = QueryArguments + "\nif ($Json) { $__powerscriptArguments.Add('--json') }" },
            "list | get | capabilities | profiles; --setting --monitor-id/--monitor-number --json"),
        Describe(
            PowerDisplayAdjust(),
            "set | up | down; --brightness/--contrast/--volume --step --monitor-id/--monitor-number"),
        Describe(
            PowerDisplayDiscrete(),
            "set --color-temperature/--input-source/--orientation --monitor-id/--monitor-number"),
        Describe(
            Template(
                "power-display-profile",
                "Power Display: apply a saved profile",
                "Apply a saved profile to connected monitors. Review its settings first: applying it can change the desktop/display. Requires the running host module.",
                "none",
                PowerDisplayProfileScript,
                Integer("ProfileId", "Positive numeric profile ID from the profiles query, not a profile name or GUID.", 1, 1))
            with { WritesState = true, Output = "none", OutputScript = ActionOutput },
            "apply-profile <positive numeric ID>"),
    ];

    private PowerShellCliTemplate PowerDisplayAdjust() =>
        Template(
            "power-display-adjust-action",
            "Power Display: adjust one or multiple monitors",
            "Set or adjust brightness, contrast or volume on selected monitors. Monitor ID takes precedence; comma-separated positive indices support batches. No power-off or input-source change.",
            "none",
            "$__powerscriptArguments.Add($Command)\n" + DisplayTargets + PowerDisplayAdjustActionScript,
            Text("Command", "Set an absolute value or adjust by a step.", "set", "set", "up", "down"),
            Text("Setting", "Continuous setting.", "brightness", "brightness", "contrast", "volume"),
            Integer("Value", "Absolute value for set.", 50, 0, 100),
            Integer("Step", "Relative increment for up/down.", 5, 0),
            Text("MonitorId", "Optional stable ID; overrides MonitorNumbers."),
            Text("MonitorNumbers", "Comma-separated positive monitor indices; empty lets the CLI select displays.", "1"))
            with
        { WritesState = true, Output = "none", OutputScript = ActionOutput };

    private PowerShellCliTemplate PowerDisplayDiscrete() =>
        Template(
            "power-display-discrete-action",
            "Power Display: change discrete settings on selected monitors",
            "Change color temperature, input source or orientation. Input/orientation changes may disrupt the desktop; query capabilities first. No monitor power-off.",
            "none",
            "$__powerscriptArguments.Add('set')\n" + DisplayTargets + PowerDisplayDiscreteActionScript,
            Text("Setting", "Query monitor capabilities before choosing a discrete value.", "orientation", "color-temperature", "input-source", "orientation"),
            Text("Value", "Orientation angle or supported hexadecimal VCP value.", "0"),
            Text("MonitorId", "Optional stable ID; overrides MonitorNumbers."),
            Text("MonitorNumbers", "Comma-separated positive monitor indices; empty lets the CLI select displays.", "1"))
            with
        { WritesState = true, Output = "none", OutputScript = ActionOutput };
}
