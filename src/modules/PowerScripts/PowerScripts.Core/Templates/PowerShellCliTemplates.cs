// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using PowerScripts.Core.Cli;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Templates;

public sealed record PowerShellCliTemplate(
    string Id,
    string Name,
    string Description,
    string Input,
    string Output,
    IReadOnlyList<ScriptTemplateParameter> Parameters,
    [property: JsonIgnore] string Executable,
    [property: JsonIgnore] string Arguments,
    ScriptTemplateParameter? InputArgument = null,
    string? HostExecutionNotice = null,
    [property: JsonIgnore] string? StandardInput = null,
    [property: JsonIgnore] string? OutputScript = null)
{
    public string ModuleId { get; init; } = string.Empty;

    public bool SupportsFileBatch { get; init; }

    public string Group { get; init; } = string.Empty;

    public string IconName { get; init; } = "PowerToys";

    internal string CommandSynopsis { get; init; } = string.Empty;

    internal bool WritesState { get; init; }

    internal bool UsesNetwork { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PowerShellCliBinding(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] JsonElement Value = default);

/// <summary>Editable PowerShell content, separate from the controlled data-only operation provider.</summary>
public static partial class PowerShellCliTemplates
{
    public const string Guide = """

        ## PowerToys module CLI content

        This is editable PowerShell, not a closed PowerToys operation. Review the script and
        parameter defaults before approving it. Creating it does not execute a command,
        approve trust, or change any sandbox, least-privilege or network setting.

        PowerShell 7 or later and the selected module CLI must already be installed.
        The executable directory is pinned to this installation. With MXC on, the Host uses
        its own installation root; editing a script literal cannot redirect the host action.
        With MXC off, the generated script's literal selects the installation.
        Executables are never selected through PATH or a runtime parameter.
        For each argument, creation can save a fixed value, use the primary script input, or
        expose a named script parameter. Only exposed parameters appear in the descriptor.
        Fixed values are stored in the reviewed script and descriptor; do not put secrets in them.
        Arguments remain individual native arguments; no command string is evaluated.

        With MXC enabled, the script runs inside the sandbox and submits one typed request.
        The Host executes the approved built-in CLI recipe outside MXC with standard-user
        permissions. Host CLI actions are not covered by the script's filesystem, network
        or desktop restrictions. The hostPowerToysCli capability and hostCli bindings in
        the descriptor are part of trust review; creating a script does not approve them.
        The Host accepts only that recipe and its reviewed fixed values/current consumer
        input, never a caller-selected executable, command string or script body.
        Edits to the brokered operation require matching descriptor changes and new approval.
        This is a terminal, one-request handoff: the built-in recipe owns native argument
        construction and output processing. Editable script code cannot resume after the
        host action, request multiple actions, or supply host-side postprocessing.
        Arbitrary Process.Start calls in script code remain inside MXC. No retry or fallback
        is performed after a contained launch or host action fails.

        Commands can modify theme/layout/display settings, write files, keep the machine awake,
        launch applications, or send content to a configured AI provider. Read the selected
        command description. File Locksmith process termination, forced monitor power-off,
        elevation controls and arbitrary command passthrough are intentionally not scaffolded.
        Additional custom parameters are available for your own edits; they are not appended
        to the CLI arguments automatically. File-producing recipes return new workspace
        files through the PowerScripts files-output contract; the consumer owns saving them.
        Queries and text transformations return text, including intentionally structured JSON.
        Action-only recipes have no primary output; native status remains diagnostic stderr.
        Inline image/audio envelopes are not interchangeable with file paths. See
        CLI-TEMPLATE-SCENARIOS.md in the source tree for supported contracts and review items.

        """;

    internal static string CreateGuide(string id)
    {
        var notice = Get(id).HostExecutionNotice;
        return Guide + (notice is null ? string.Empty : "\n### Approved host execution\n\n" + notice + "\n");
    }

    internal static ScriptTemplateOptions Configure(ScriptTemplateOptions options)
    {
        var template = Get(options.CliContent);
        var definitions = Arguments(template);
        var bindings = ResolveBindings(template, options.CliBindings);
        var inputTypes = definitions.Where(parameter => bindings[parameter.Name].Source == "input")
            .Select(InputType).Distinct(StringComparer.Ordinal).ToArray();
        if (inputTypes.Length > 1)
        {
            throw new ArgumentException("A script has one primary input: use either text or files, not both.", nameof(options));
        }

        var input = inputTypes.SingleOrDefault() ?? "none";
        if (options.Runtime != "powershell" || options.Input != input || options.Output != template.Output)
        {
            throw new ArgumentException($"This module-call configuration requires PowerShell, {input} input and {template.Output} output.", nameof(options));
        }

        var names = definitions.Select(parameter => parameter.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (options.Parameters?.Any(parameter => parameter is not null &&
            (names.Contains(parameter.Name) || (input == "files" && parameter.Name.Equals("Files", StringComparison.OrdinalIgnoreCase)) ||
                (input == "text" && parameter.Name.Equals("Text", StringComparison.OrdinalIgnoreCase)))) == true)
        {
            throw new ArgumentException("Custom parameter names must not conflict with module arguments or the primary input.", nameof(options));
        }

        if (input == "files" && !template.SupportsFileBatch &&
            (options.MinFiles != 1 || options.MaxFiles > 1))
        {
            throw new ArgumentException("This CLI template accepts exactly one selected file.", nameof(options));
        }

        return options with
        {
            Parameters = definitions.Where(parameter => bindings[parameter.Name].Source == "parameter").Concat(options.Parameters ?? []).ToArray(),
            Description = string.IsNullOrWhiteSpace(options.Description) ? template.Description : options.Description,
            MaxFiles = input == "files" && !template.SupportsFileBatch && options.MaxFiles == 0 ? 1 : options.MaxFiles,
        };
    }

    internal static HostCliPermission CreateHostPermission(ScriptTemplateOptions options) =>
        new() { Id = Get(options.CliContent).Id, Bindings = ResolveBindings(Get(options.CliContent), options.CliBindings) };

    internal static IReadOnlyList<string> HostCapabilities(string id)
    {
        var template = Get(id);
        var capabilities = new List<string> { "fileRead", HostCliBroker.Capability };
        if (template.Output == "files" || template.WritesState)
        {
            capabilities.Add("fileWrite");
        }

        if (template.UsesNetwork)
        {
            capabilities.Add("network");
        }

        return capabilities;
    }

    internal static string CreateScript(ScriptTemplateOptions options, bool hostExecution = false, string? installationRoot = null)
    {
        var template = Get(options.CliContent);
        var bindings = ResolveBindings(template, options.CliBindings);
        var declarations = new List<string>();
        if (options.Input == "files")
        {
            declarations.Add("[string[]]$Files");
        }
        else if (options.Input == "text")
        {
            declarations.Add("[string]$Text");
        }

        foreach (var parameter in options.Parameters ?? [])
        {
            var schema = parameter.Schema;
            var value = schema.Default.ValueKind == JsonValueKind.Undefined ? string.Empty : " = " + Literal(schema.Default);
            declarations.Add($"{Variable(parameter)}{value}");
        }

        var assignments = new List<string>();
        foreach (var parameter in Arguments(template))
        {
            var binding = bindings[parameter.Name];
            if (binding.Source == "parameter" || (parameter == template.InputArgument && binding.Source == "input" && template.Input == "files"))
            {
                continue;
            }

            var value = binding.Source == "fixed" ? Literal(binding.Value) : InputType(parameter) == "files" ? "$Files[0]" : "$Text";
            if (binding.Source == "input" && parameter.Schema.Type is "integer" or "boolean")
            {
                var type = parameter.Schema.Type == "integer" ? "int" : "bool";
                var arguments = type == "int" ? "$Text, [Globalization.NumberStyles]::Integer, [Globalization.CultureInfo]::InvariantCulture, " : "$Text, ";
                assignments.Add($"$__powerscriptParsed = {(type == "int" ? "0" : "$false")}");
                assignments.Add($"if (-not [{type}]::TryParse({arguments}[ref]$__powerscriptParsed)) {{ throw '{parameter.Name} requires {parameter.Schema.Type} input.' }}");
                value = "$__powerscriptParsed";
            }

            assignments.Add($"{Variable(parameter)} = {value}");
        }

        if (template.InputArgument is { } file && template.Input == "files" && bindings[file.Name].Source != "input")
        {
            assignments.Add($"if (-not (Test-Path -LiteralPath ${file.Name} -PathType Leaf)) {{ throw 'Choose an existing input file.' }}");
            assignments.Add($"$Files = @(${file.Name})");
        }

        var declaration = declarations.Count == 0 ? string.Empty : "param(\n    " + string.Join(",\n    ", declarations) + "\n)";
        var redirectInput = template.StandardInput is null ? string.Empty : """
            $__powerscriptStartInfo.RedirectStandardInput = $true
            $__powerscriptStartInfo.StandardInputEncoding = [Text.UTF8Encoding]::new($false)
            """;
        var writeInput = template.StandardInput is null ? string.Empty : $$"""
            $__powerscriptProcess.StandardInput.Write({{template.StandardInput}})
            $__powerscriptProcess.StandardInput.Close()
            """;
        var hostRequest = hostExecution ? string.Empty : $$"""
            if ($env:{{HostCliBroker.EnvironmentVariable}}) {
                $__powerscriptHostValues = @{
                    {{string.Join(Environment.NewLine, Arguments(template).Select(parameter =>
                        Quote(parameter.Name) + " = " +
                        (parameter == template.InputArgument && template.Input == "files" ? "$Files[0]" : "$" + parameter.Name)))}}
                }
                [Console]::Write((@{
                    protocolVersion = 1
                    id = {{Quote(template.Id)}}
                    token = $env:{{HostCliBroker.EnvironmentVariable}}
                    values = $__powerscriptHostValues
                    files = {{(template.Input == "files" ? "@($Files)" : "@()")}}
                } | ConvertTo-Json -Depth 8 -Compress))
                return
            }
            """;
        return $$"""
            # Review the command and its side effects before approving; see README.md.
            {{declaration}}
            $ErrorActionPreference = 'Stop'
            [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
            if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'This module CLI template requires PowerShell 7 or later.' }
            $__powerscriptExecutable = Join-Path {{Quote(installationRoot ?? InstallationDirectory())}} {{Quote(template.Executable)}}
            if (-not (Test-Path -LiteralPath $__powerscriptExecutable -PathType Leaf)) { throw "Module CLI is not installed: $__powerscriptExecutable" }
            if ($env:POWERSCRIPTS_FILES) { $Files = @($env:POWERSCRIPTS_FILES -split "`n" | Where-Object { $_ }) }
            {{string.Join(Environment.NewLine, assignments)}}
            {{hostRequest}}
            $__powerscriptArguments = [Collections.Generic.List[string]]::new()
            {{template.Arguments}}

            $__powerscriptStartInfo = [Diagnostics.ProcessStartInfo]::new()
            $__powerscriptStartInfo.FileName = $__powerscriptExecutable
            $__powerscriptStartInfo.UseShellExecute = $false
            $__powerscriptStartInfo.RedirectStandardOutput = $true
            $__powerscriptStartInfo.RedirectStandardError = $true
            $__powerscriptStartInfo.StandardOutputEncoding = [Text.UTF8Encoding]::new($false)
            $__powerscriptStartInfo.StandardErrorEncoding = [Text.UTF8Encoding]::new($false)
            {{redirectInput}}
            foreach ($__powerscriptArgument in $__powerscriptArguments) { $__powerscriptStartInfo.ArgumentList.Add($__powerscriptArgument) }
            $__powerscriptProcess = [Diagnostics.Process]::new()
            $__powerscriptProcess.StartInfo = $__powerscriptStartInfo
            try {
                if (-not $__powerscriptProcess.Start()) { throw 'Unable to start module CLI.' }
                $__powerscriptStdout = $__powerscriptProcess.StandardOutput.ReadToEndAsync()
                $__powerscriptStderr = $__powerscriptProcess.StandardError.ReadToEndAsync()
                {{writeInput}}
                $__powerscriptProcess.WaitForExit()
                $__powerscriptOutput = $__powerscriptStdout.GetAwaiter().GetResult()
                [Console]::Error.Write($__powerscriptStderr.GetAwaiter().GetResult())
                if ($__powerscriptProcess.ExitCode -ne 0) {
                    if (-not [string]::IsNullOrWhiteSpace($__powerscriptOutput)) { [Console]::Error.WriteLine($__powerscriptOutput) }
                    throw "Module CLI failed with exit code $($__powerscriptProcess.ExitCode)."
                }
                {{template.OutputScript ?? "[Console]::Write($__powerscriptOutput)"}}
            } finally {
                $__powerscriptProcess.Dispose()
            }

            """;
    }

    internal static PowerShellCliTemplate Get(string? id) =>
        All.Concat(LegacyCatalogue).FirstOrDefault(template => string.Equals(template.Id, id, StringComparison.Ordinal))
        ?? throw new ArgumentException("Choose a known module CLI content template.", nameof(id));

    private static string Quote(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    internal static IReadOnlyList<ScriptTemplateParameter> Arguments(PowerShellCliTemplate template) =>
        template.InputArgument is { } input ? new[] { input }.Concat(template.Parameters).ToArray() : template.Parameters;

    private static string InputType(ScriptTemplateParameter parameter) => parameter.Schema.IsFileParameter ? "files" : "text";

    internal static IReadOnlyDictionary<string, PowerShellCliBinding> ResolveBindings(
        PowerShellCliTemplate template, IReadOnlyDictionary<string, PowerShellCliBinding>? configured)
    {
        var definitions = Arguments(template);
        if (configured?.Keys.Any(name => !definitions.Any(parameter => parameter.Name == name)) == true)
        {
            throw new ArgumentException("Unknown module argument in CLI bindings.", nameof(configured));
        }

        var result = new Dictionary<string, PowerShellCliBinding>(StringComparer.Ordinal);
        foreach (var parameter in definitions)
        {
            if (configured?.TryGetValue(parameter.Name, out var supplied) == true && supplied is null)
            {
                throw new ArgumentException($"A value source is required for '{parameter.Name}'.", nameof(configured));
            }

            var binding = configured?.GetValueOrDefault(parameter.Name) ??
                new PowerShellCliBinding(parameter == template.InputArgument ? "input" : "parameter");
            if (binding.Source is not ("fixed" or "input" or "parameter") ||
                (binding.Source != "fixed" && binding.Value.ValueKind != JsonValueKind.Undefined))
            {
                throw new ArgumentException($"Choose fixed, input or parameter for '{parameter.Name}'; only fixed bindings accept a value.", nameof(configured));
            }

            if (binding.Source == "fixed")
            {
                ScriptSchema.Validate(parameter.Schema, binding.Value, parameter.Name);
                if (binding.Value.ValueKind == JsonValueKind.String && binding.Value.GetString()!.Contains('\0'))
                {
                    throw new ArgumentException($"'{parameter.Name}' cannot contain NUL characters.", nameof(configured));
                }

                if (parameter.Required && binding.Value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(binding.Value.GetString()))
                {
                    throw new ArgumentException($"A fixed value is required for '{parameter.Name}'.", nameof(configured));
                }
            }

            result.Add(parameter.Name, binding);
        }

        return result;
    }

    private static string Literal(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => Quote(value.GetString()!),
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "$true",
        JsonValueKind.False => "$false",
        _ => throw new ArgumentException("Module argument values must be typed strings, integers or booleans."),
    };

    private static string Variable(ScriptTemplateParameter parameter)
    {
        var schema = parameter.Schema;
        var validation = schema.Enum is { Count: > 0 }
            ? "[ValidateSet(" + string.Join(", ", schema.Enum.Select(Quote)) + ")]"
            : schema.Type == "integer" ? $"[ValidateRange({schema.Minimum ?? int.MinValue}, {schema.Maximum ?? int.MaxValue})]" : string.Empty;
        var type = schema.Type switch { "integer" => "int", "boolean" => "bool", _ => "string" };
        return $"{validation}[{type}]${parameter.Name}";
    }

    internal static string InstallationDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory.Parent is not null && directory.Name is "PowerScripts" or "WinUI3Apps")
        {
            directory = directory.Parent;
        }

        return directory.FullName;
    }

}
