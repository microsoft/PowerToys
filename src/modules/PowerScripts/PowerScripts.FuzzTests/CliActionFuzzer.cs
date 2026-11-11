// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Text.Json;
using PowerScripts.Core.Cli;
using PowerScripts.Core.Execution;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Templates;

namespace PowerScripts.FuzzTests;

public enum FuzzInputResult
{
    Rejected,
    Accepted,
    SkippedOversize,
}

/// <summary>Pure configuration targets: no package creation, trust changes, or process execution.</summary>
public static class CliActionFuzzer
{
    // Keep an iteration bounded while still reaching each parser's smaller input limit.
    public const int MaximumInputBytes = 512 * 1024;

    public static void FuzzAction(ReadOnlySpan<byte> input) => _ = ParseAndValidateAction(input);

    public static void FuzzTemplateConfiguration(ReadOnlySpan<byte> input) => _ = ParseTemplateConfiguration(input);

    public static FuzzInputResult ParseAndValidateAction(ReadOnlySpan<byte> input)
    {
        if (input.Length > MaximumInputBytes)
        {
            return FuzzInputResult.SkippedOversize;
        }

        try
        {
            var action = CliActionProvider.Parse(Encoding.UTF8.GetString(input));
            var errors = CliActionProvider.Validate(CreateManifest(), action);
            return errors.Count == 0 ? FuzzInputResult.Accepted : FuzzInputResult.Rejected;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            // Expected rejected inputs are harmless. All unexpected exceptions remain visible
            // to the fuzzing engine instead of being mistaken for configuration errors.
            return FuzzInputResult.Rejected;
        }
    }

    public static FuzzInputResult ParseTemplateConfiguration(ReadOnlySpan<byte> input)
    {
        if (input.Length > MaximumInputBytes)
        {
            return FuzzInputResult.SkippedOversize;
        }

        try
        {
            _ = TemplateConfiguration.Parse(Encoding.UTF8.GetString(input));
            return FuzzInputResult.Accepted;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            return FuzzInputResult.Rejected;
        }
    }

    private static PowerScriptManifest CreateManifest() => new()
    {
        Id = "fuzz-file-query",
        Name = "Fuzz file query",
        Entry = "action.json",
        Runtime = ScriptRuntime.PowerToysOperation,
        Execute = new ScriptExecute { Provider = CliActionProvider.ProviderName },
        InputFormat = PowerScriptDataFormat.None,
        OutputFormat = PowerScriptDataFormat.Text,
        Capabilities = new List<string> { "fileRead" },
        Parameters = new List<ScriptParameter>
        {
            new() { Name = "path", Type = ScriptParameter.ParameterTypeFile, IsRequired = true },
        },
        InputSchema = new JsonSchemaObject
        {
            Type = "object",
            AdditionalProperties = false,
            Properties = new Dictionary<string, JsonSchemaProperty>
            {
                ["parameters"] = new()
                {
                    Type = "object",
                    AdditionalProperties = false,
                    Required = new List<string> { "path" },
                    Properties = new Dictionary<string, JsonSchemaProperty>
                    {
                        ["path"] = new() { Type = "string", Format = JsonSchemaProperty.FilePathFormat },
                    },
                },
            },
            Required = new List<string> { "parameters" },
        },
        OutputSchema = new JsonSchemaObject
        {
            Type = "object",
            AdditionalProperties = false,
            Required = new List<string> { "text" },
            Properties = new Dictionary<string, JsonSchemaProperty>
            {
                ["text"] = new() { Type = "string" },
            },
        },
    };
}
