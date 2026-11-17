// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PowerScripts.Core.Execution;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Security;
using PowerScripts.Core.Templates;

namespace PowerScripts.Core.Cli;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class HostCliPermission
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("bindings")]
    public IReadOnlyDictionary<string, PowerShellCliBinding> Bindings { get; set; } =
        new Dictionary<string, PowerShellCliBinding>();
}

/// <summary>Authorizes one catalogue recipe against the reviewed descriptor and current input.</summary>
internal static class HostCliBroker
{
    internal const string Capability = "hostPowerToysCli";
    internal const string EnvironmentVariable = "POWERSCRIPTS_HOST_CLI_REQUEST";
    internal const int MaximumRequestBytes = 8 * 1024 * 1024;

    internal static IReadOnlyList<string> Validate(PowerScriptManifest manifest)
    {
        if (manifest.HostCli is not { } permission)
        {
            return manifest.Capabilities.Contains(Capability, StringComparer.Ordinal)
                ? ["The hostPowerToysCli capability requires a hostCli recipe and bindings."]
                : [];
        }

        try
        {
            if (manifest.Runtime != ScriptRuntime.PowerShell || manifest.Execute is not null ||
                !manifest.Capabilities.Contains(Capability, StringComparer.Ordinal) || permission.Bindings is null)
            {
                throw new InvalidDataException("Host CLI access requires a PowerShell script without a custom launch recipe, hostCli bindings and the hostPowerToysCli capability.");
            }

            var options = PowerShellCliTemplates.Configure(new ScriptTemplateOptions(
                "Host CLI permission",
                Input: ScriptIo.ToToken(manifest.InputFormat),
                Output: ScriptIo.ToToken(manifest.OutputFormat),
                MinFiles: manifest.Input?.MinFiles ?? 1,
                MaxFiles: manifest.Input?.MaxFiles ?? 0,
                CliContent: permission.Id,
                CliBindings: permission.Bindings));
            if (PowerShellCliTemplates.HostCapabilities(permission.Id).Any(capability =>
                !manifest.Capabilities.Contains(capability, StringComparer.Ordinal)))
            {
                throw new InvalidDataException("The descriptor must declare every capability required by its host CLI recipe.");
            }

            if (options.Parameters!.Any(parameter =>
                !manifest.Parameters.Any(exposed => exposed.Name == parameter.Name)))
            {
                throw new InvalidDataException("Every runtime host CLI binding must reference a declared script parameter.");
            }

            return [];
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException)
        {
            return [$"Invalid host CLI permission: {ex.Message}"];
        }
    }

    internal static HostCliInvocation? CreateInvocation(
        PowerScriptManifest manifest,
        IReadOnlyList<string> files,
        IReadOnlyDictionary<string, string?> parameters,
        string? text)
    {
        var errors = Validate(manifest);
        if (errors.Count > 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        }

        if (manifest.HostCli is not { } permission)
        {
            return null;
        }

        var definition = PowerShellCliTemplates.Get(permission.Id);
        var bindings = PowerShellCliTemplates.ResolveBindings(definition, permission.Bindings);
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var argument in PowerShellCliTemplates.Arguments(definition))
        {
            var binding = bindings[argument.Name];
            var value = binding.Source switch
            {
                "fixed" => binding.Value.Clone(),
                "input" => ReadValue(argument.Schema, argument.Schema.IsFileParameter ? files.FirstOrDefault() : text),
                _ => ReadValue(argument.Schema, parameters.GetValueOrDefault(argument.Name)),
            };
            ScriptSchema.Validate(argument.Schema, value, argument.Name);
            if (value.ValueKind == JsonValueKind.String && value.GetString()!.Contains('\0'))
            {
                throw new InvalidDataException($"Host CLI argument '{argument.Name}' contains a NUL character.");
            }

            values.Add(argument.Name, value);
        }

        var authorizedFiles = definition.Input == "files"
            ? bindings[definition.InputArgument!.Name].Source == "input"
                ? files.ToArray()
                : [values[definition.InputArgument.Name].GetString()!]
            : Array.Empty<string>();
        return new HostCliInvocation(definition, values, authorizedFiles, PowerShellCliTemplates.InstallationDirectory());
    }

    private static JsonElement ReadValue(JsonSchemaProperty schema, string? value) => schema.Type switch
    {
        "integer" when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) =>
            JsonSerializer.SerializeToElement(number),
        "boolean" when bool.TryParse(value, out var flag) => JsonSerializer.SerializeToElement(flag),
        "string" => JsonSerializer.SerializeToElement(value ?? string.Empty),
        _ => throw new InvalidDataException("The host CLI argument does not match its catalogue type."),
    };
}

internal sealed class HostCliInvocation
{
    private readonly PowerShellCliTemplate _definition;
    private readonly IReadOnlyDictionary<string, JsonElement> _values;
    private readonly string[] _files;
    private readonly string _installationRoot;
    private int _consumed;

    internal HostCliInvocation(
        PowerShellCliTemplate definition,
        IReadOnlyDictionary<string, JsonElement> values,
        string[] files,
        string installationRoot)
    {
        _definition = definition;
        _values = values.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal);
        _files = files.ToArray();
        _installationRoot = installationRoot;
    }

    internal string Token { get; } = Guid.NewGuid().ToString("N");

    internal ProcessRunResult Run(
        string requestJson,
        string workspace,
        int timeoutMs,
        Func<ProcessStartInfo, string?, int, ProcessRunResult>? run = null)
    {
        try
        {
            ValidateRequest(requestJson);
            if (Interlocked.Exchange(ref _consumed, 1) != 0)
            {
                throw new InvalidDataException("The host CLI permission has already been consumed.");
            }

            if (!CliExecutableResolver.TryResolve(_installationRoot, _definition.Executable, out var executable, out var reason))
            {
                throw new InvalidDataException(reason);
            }

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!CliExecutableResolver.TryResolve(programFiles, @"PowerShell\7\pwsh.exe", out var interpreter, out reason))
            {
                throw new InvalidDataException($"The host CLI broker requires the installed PowerShell 7 runtime: {reason}");
            }

            // Hold both images against replacement until the fixed recipe and its child have exited.
            using var executableLease = new FileStream(executable!, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var interpreterLease = new FileStream(interpreter!, FileMode.Open, FileAccess.Read, FileShare.Read);
            var script = PowerShellCliTemplates.CreateScript(new ScriptTemplateOptions(
                "Host CLI implementation",
                Input: _definition.Input,
                Output: _definition.Output,
                Parameters: _definition.Parameters,
                CliContent: _definition.Id), hostExecution: true, installationRoot: _installationRoot);
            var command = """
                $ErrorActionPreference = 'Stop'
                [Console]::InputEncoding = [Text.UTF8Encoding]::new($false)
                $hostRequest = ConvertFrom-Json -InputObject ([Console]::In.ReadToEnd())
                $hostArguments = @{}
                foreach ($property in $hostRequest.parameters.PSObject.Properties) {
                    $hostArguments[$property.Name] = $property.Value
                }
                """ + Environment.NewLine +
                (_definition.Input == "files" ? "$hostArguments['Files'] = [string[]]$hostRequest.files" + Environment.NewLine : string.Empty) +
                (_definition.Input == "text" ? "$hostArguments['Text'] = $hostRequest.text" + Environment.NewLine : string.Empty) +
                "& {" + Environment.NewLine + script + Environment.NewLine + "} @hostArguments";
            var start = new ProcessStartInfo(interpreter!)
            {
                WorkingDirectory = workspace,
                StandardInputEncoding = new UTF8Encoding(false),
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-NonInteractive");
            start.ArgumentList.Add("-EncodedCommand");
            start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(command)));
            start.Environment.Remove(HostCliBroker.EnvironmentVariable);
            start.Environment.Remove(ScriptExecutor.FilesEnvironmentVariable);
            start.Environment[MxcProcessRunner.WorkspaceEnvironmentVariable] = workspace;
            var input = JsonSerializer.Serialize(new
            {
                parameters = _definition.Parameters.ToDictionary(parameter => parameter.Name, parameter => _values[parameter.Name]),
                files = _files,
                text = _definition.Input == "text" ? _values[_definition.InputArgument!.Name].GetString() : null,
            });
            return (run ?? ProcessRunner.SecureLauncher.Run)(start, input, timeoutMs);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or
            UnauthorizedAccessException or ArgumentException or Win32Exception)
        {
            return new ProcessRunResult(2, string.Empty, $"PowerScripts refused the host CLI request: {ex.Message}");
        }
    }

    internal void ValidateRequest(string json)
    {
        if (json.Length > HostCliBroker.MaximumRequestBytes || Encoding.UTF8.GetByteCount(json) > HostCliBroker.MaximumRequestBytes)
        {
            throw new InvalidDataException("The host CLI request exceeds 8 MiB.");
        }

        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
        CliActionProvider.CheckDuplicateProperties(document.RootElement);
        var request = JsonSerializer.Deserialize<HostCliRequest>(json, ToolDescriptorParser.JsonOptions)
            ?? throw new InvalidDataException("A host CLI request must be an object.");
        if (request.ProtocolVersion != 1 || request.Id != _definition.Id || request.Token != Token ||
            request.Values is null || request.Values.Count != _values.Count ||
            request.Files is null || !request.Files.SequenceEqual(_files, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The request does not match this execution's approved host CLI recipe and input.");
        }

        foreach (var (name, value) in _values)
        {
            if (!request.Values.TryGetValue(name, out var actual) || !JsonElement.DeepEquals(value, actual))
            {
                throw new InvalidDataException($"Host CLI argument '{name}' differs from its reviewed binding or current consumer input.");
            }
        }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class HostCliRequest
    {
        public int ProtocolVersion { get; set; }

        public string? Id { get; set; }

        public string? Token { get; set; }

        public Dictionary<string, JsonElement>? Values { get; set; }

        public string[]? Files { get; set; }
    }
}
