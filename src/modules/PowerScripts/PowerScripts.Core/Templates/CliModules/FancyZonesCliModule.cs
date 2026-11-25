// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using static PowerScripts.Core.Templates.CliRecipeBuilder;

namespace PowerScripts.Core.Templates.CliModules;

internal sealed class FancyZonesCliModule : PowerShellCliModule
{
    private const string Arguments = """
            $__powerscriptArguments.Add($Command)
            switch ($Command) {
                'set-layout' {
                    if (-not $Layout.Trim()) { throw 'Set-layout requires Layout.' }
                    $__powerscriptArguments.Add($Layout)
                    if ($AllMonitors) { $__powerscriptArguments.Add('--all') }
                    else { $__powerscriptArguments.AddRange([string[]]@('--monitor', $Monitor.ToString([Globalization.CultureInfo]::InvariantCulture))) }
                }
                'set-hotkey' {
                    if (-not $Layout.Trim()) { throw 'Set-hotkey requires a layout UUID.' }
                    $__powerscriptArguments.AddRange([string[]]@($Hotkey.ToString([Globalization.CultureInfo]::InvariantCulture), $Layout))
                }
                'remove-hotkey' { $__powerscriptArguments.Add($Hotkey.ToString([Globalization.CultureInfo]::InvariantCulture)) }
            }
            """;

    public override string Id => "fancyzones";

    public override string Name => "FancyZones";

    public override string IconName => "FancyZones";

    public override string Executable => @"FancyZonesCLI.exe";

    public override int Order => 30;

    public override IReadOnlyList<PowerShellCliTemplate> CreateRecipes() =>
    [
        Describe(Variant(Arguments, "fancyzones-query", "FancyZones: query layouts, monitors or hotkeys",
            "Read layout/monitor/hotkey information. Requires the running host module and any saved layout data.", "text", string.Empty,
            Text("Command", "Read-only query.", "get-layouts", "get-layouts", "get-monitors", "get-active-layout", "get-hotkeys")),
            "get-layouts | get-monitors | get-active-layout | get-hotkeys"),
        Describe(Variant(Arguments, "fancyzones-layout", "FancyZones: apply a layout",
            "Apply a saved layout by name or UUID to one or all monitors. Changes desktop layout settings.", "none", "$Command = 'set-layout'",
            Text("Layout", "Required saved layout name or UUID."),
            Integer("Monitor", "One-based monitor index, ignored when AllMonitors is enabled.", 1, 1),
            Flag("AllMonitors", "Apply to every monitor.")),
            "set-layout <name-or-UUID> --monitor <n> | --all"),
        Describe(Variant(Arguments, "fancyzones-hotkey", "FancyZones: assign or remove a layout hotkey",
            "Change saved layout hotkeys; not a report or automatic replay.", "none", string.Empty,
            Text("Command", "Assign a layout UUID or remove the selected digit.", "set-hotkey", "set-hotkey", "remove-hotkey"),
            Text("Layout", "Layout UUID required by set-hotkey; ignored by remove-hotkey."),
            Integer("Hotkey", "Hotkey digit.", 1, 0, 9)),
            "set-hotkey <digit> <UUID> | remove-hotkey <digit>"),
        Describe(Variant(Arguments, "fancyzones-open", "FancyZones: open editor or settings",
            "Launch the requested UI on the host desktop.", "none", string.Empty,
            Text("Command", "UI to open.", "open-editor", "open-editor", "open-settings")),
            "open-editor | open-settings"),
    ];
}
