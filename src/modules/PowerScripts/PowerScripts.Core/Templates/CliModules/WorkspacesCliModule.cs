// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using static PowerScripts.Core.Templates.CliRecipeBuilder;

namespace PowerScripts.Core.Templates.CliModules;

internal sealed class WorkspacesCliModule : PowerShellCliModule
{
    private const string Arguments = """
            if ($WorkspaceId -and $WorkspaceName) { throw 'Specify WorkspaceId or WorkspaceName, not both.' }
            if ($Command -eq 'launch' -and -not ($WorkspaceId -or $WorkspaceName)) { throw 'Launch requires a workspace selector.' }
            $__powerscriptArguments.Add($Command)
            if ($WorkspaceId) { $__powerscriptArguments.AddRange([string[]]@('--id', $WorkspaceId)) }
            if ($WorkspaceName) { $__powerscriptArguments.AddRange([string[]]@('--name', $WorkspaceName)) }
            if ($Command -eq 'list' -and $Details) { $__powerscriptArguments.Add('--details') }
            if ($Command -eq 'launch') { $__powerscriptArguments.AddRange([string[]]@('--timeout', $TimeoutSeconds.ToString([Globalization.CultureInfo]::InvariantCulture))) }
            if ($Json) { $__powerscriptArguments.Add('--json') }
            """;

    public override string Id => "workspaces";

    public override string Name => "Workspaces";

    public override string IconName => "Workspaces";

    public override string Executable => @"PowerToys.WorkspacesCLI.exe";

    public override int Order => 40;

    public override IReadOnlyList<PowerShellCliTemplate> CreateRecipes() =>
    [
        Describe(
            Variant(
                Arguments,
                "workspaces-query",
                "Workspaces: list saved workspaces",
                "Read workspace summaries or application details, optionally filtered by ID or name.",
                "text",
                "$Command = 'list'",
                Text("WorkspaceId", "Optional GUID; mutually exclusive with WorkspaceName."),
                Text("WorkspaceName", "Optional name; mutually exclusive with WorkspaceId."),
                Flag("Details", "Include application details."),
                Flag("Json", "Return structured JSON text.", true)),
            "list [--id | --name] [--details] [--json]"),
        Describe(
            Variant(
                Arguments,
                "workspaces-launch",
                "Workspaces: launch a saved workspace",
                "Launch applications and arrange windows. Choose ID or name; execution can show prompts and is not replay-safe.",
                "none",
                "$Command = 'launch'",
                Text("WorkspaceId", "GUID; specify this or WorkspaceName."),
                Text("WorkspaceName", "Name; specify this or WorkspaceId."),
                Integer("TimeoutSeconds", "Native launch timeout; the Host execution budget still applies.", 120, 1, 600)),
            "launch --id | --name [--timeout]"),
    ];
}
