// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using static PowerScripts.Core.Templates.CliRecipeBuilder;

namespace PowerScripts.Core.Templates.CliModules;

internal sealed class SettingsCliModule : PowerShellCliModule
{
    public override string Id => "settings";

    public override string Name => "Settings";

    public override string Executable => @"WinUI3Apps\PowerToys.Settings.Cli.exe";

    public override int Order => 0;

    public override IReadOnlyList<PowerShellCliTemplate> CreateRecipes() =>
    [
        Describe(Template("settings-query", "Settings: list modules or check enabled status",
            "Read saved module state and Group Policy overrides. Status does not prove that a module's process is running.", "none",
            """
            $__powerscriptArguments.Add($Command)
            if ($Command -eq 'status') {
                if (-not $ModuleName.Trim()) { throw 'Status requires ModuleName.' }
                $__powerscriptArguments.Add($ModuleName)
            }
            if ($Json) { $__powerscriptArguments.Add('--json') }
            """,
            Text("Command", "List all modules or query one module.", "list", "list", "status"),
            Text("ModuleName", "Module name for status, such as Awake, FancyZones or LightSwitch.", "Awake"),
            Flag("Json", "Return structured module state.", true)),
            "list | status <module> [--json]"),
        Describe(AsAction(Template("settings-module-state", "Settings: save a module's enabled or disabled state",
            "Saves module state for the next PowerToys start; it does not live-toggle modules. The current Settings CLI requires PowerToys to be stopped, so this cannot currently enable a module from a PowerToys hotkey. Group Policy is respected.", "none",
            """
            if (-not $ModuleName.Trim()) { throw 'Choose a ModuleName.' }
            $__powerscriptArguments.AddRange([string[]]@($Command, $ModuleName))
            """,
            Text("Command", "Save the module as enabled or disabled for the next PowerToys start.", "enable", "enable", "disable"),
            Text("ModuleName", "Module name to change, such as Awake, FancyZones or LightSwitch.", "Awake")) with { Id = "settings-module-action" }),
            "enable | disable <module>"),
    ];
}
