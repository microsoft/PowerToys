// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Templates;

internal static class CliRecipeBuilder
{
    internal static PowerShellCliTemplate Describe(PowerShellCliTemplate template, string commandSynopsis) =>
        template with { CommandSynopsis = commandSynopsis };

    internal static PowerShellCliTemplate AsAction(PowerShellCliTemplate template, bool writesState = true) =>
        template with { Output = "none", OutputScript = ActionOutput, WritesState = writesState };

    internal const string ActionOutput = """
        if (-not [string]::IsNullOrWhiteSpace($__powerscriptOutput)) { [Console]::Error.WriteLine($__powerscriptOutput) }
        """;

    internal const string WorkspaceDirectory = """
        $__powerscriptWorkspace = $env:POWERSCRIPTS_MXC_WORKSPACE
        if (-not $__powerscriptWorkspace) { $__powerscriptWorkspace = [IO.Path]::GetTempPath() }
        $__powerscriptResultDirectory = Join-Path $__powerscriptWorkspace ('powerscript-transform-' + [Guid]::NewGuid().ToString('N'))
        [IO.Directory]::CreateDirectory($__powerscriptResultDirectory) | Out-Null
        """;

    internal const string FileOutput = """
        if (-not [string]::IsNullOrWhiteSpace($__powerscriptOutput)) { [Console]::Error.WriteLine($__powerscriptOutput) }
        if (-not [IO.File]::Exists($__powerscriptResultFile)) { throw 'The CLI did not produce the requested output file.' }
        [Console]::Write((@{ files = @($__powerscriptResultFile) } | ConvertTo-Json -Compress))
        """;

    internal static PowerShellCliTemplate Template(string id, string name, string description, string input, string executable, string arguments, params ScriptTemplateParameter[] parameters)
        => new(id, name, description, input, "text", parameters, executable, arguments,
            input is "files" or "text" ? new(input == "files" ? "SourceFile" : "Content", new JsonSchemaProperty
            {
                Type = "string",
                Format = input == "files" ? JsonSchemaProperty.FilePathFormat : null,
                Title = input == "files" ? "Input file" : "Input text",
                Description = input == "files"
                    ? "Use selected files as script input, save one fixed file path, or ask for one file when the script runs."
                    : "Use script text input, save fixed text, or ask for text when the script runs.",
                MinLength = 1,
            }, Required: true) : null,
            HostExecutionNotice: "With MXC enabled, script code remains contained, but this approved CLI recipe runs on the host with your standard-user permissions. " +
                "The host action is outside the script's filesystem, network and desktop restrictions. " +
                (id == "settings-module-state" ? "The Settings CLI still requires PowerToys to be stopped; this does not bypass Group Policy or the Runner's settings lock." :
                    "Review the descriptor's hostCli bindings and the action's side effects before approval."));

    internal static ScriptTemplateParameter Text(string name, string description, string value = "", params string[] choices) =>
        new(name, new JsonSchemaProperty
        {
            Type = "string",
            Title = name,
            Description = description,
            Default = JsonSerializer.SerializeToElement(value),
            Enum = choices.Length == 0 ? null : choices.ToList(),
        });

    internal static ScriptTemplateParameter Integer(string name, string description, int value, int minimum = 0, int maximum = int.MaxValue) =>
        new(name, new JsonSchemaProperty
        {
            Type = "integer",
            Title = name,
            Description = description,
            Default = JsonSerializer.SerializeToElement(value),
            Minimum = minimum,
            Maximum = maximum,
        });

    internal static ScriptTemplateParameter Flag(string name, string description, bool value = false) =>
        new(name, new JsonSchemaProperty { Type = "boolean", Title = name, Description = description, Default = JsonSerializer.SerializeToElement(value) });
}
