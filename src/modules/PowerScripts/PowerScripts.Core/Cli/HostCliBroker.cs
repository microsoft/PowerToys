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
                "input" => ReadValue(argument.Schema, argument.Schema.IsFileParameter ? files.Count > 0 ? files[0] : null : text),
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
