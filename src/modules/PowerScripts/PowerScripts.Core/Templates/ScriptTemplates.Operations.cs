// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using PowerScripts.Core.Cli;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Security;

namespace PowerScripts.Core.Templates;

/// <summary>Instantiates a known operation without running it, checking deployment or granting trust.</summary>
public sealed record PowerToysOperationTemplateOptions(
    string CommandId,
    IReadOnlyDictionary<string, CliActionBinding>? Bindings = null);

public static partial class ScriptTemplates
{
    public const string OperationTemplatePrefix = "powertoys:";

    /// <summary>The operation catalogue is exposed through the existing template discovery command.</summary>
    public static IReadOnlyList<ScriptTemplate> OperationTemplates
    {
        get
        {
            var settings = MxcSettings.Load();
            return BuiltinCliCatalog.Load().Commands.Select(command => new ScriptTemplate(
                OperationTemplatePrefix + command.Id,
                command.Name,
                command.Description,
                Group: command.Group,
                CommandId: command.Id,
                Parameters: command.Parameters.Select(parameter => ToTemplateParameter(command, parameter)).ToArray(),
                UnavailableReason: CliActionProvider.GetUnavailableReason(command, settings),
                ModuleId: command.ModuleId)).ToArray();
        }
    }

    /// <summary>Includes the existing educational templates and the controlled PowerToys operations.</summary>
    public static IReadOnlyList<ScriptTemplate> Available => All.Concat(OperationTemplates).ToArray();

    private static CreatedScriptTemplate CreateOperation(string scriptsRoot, ScriptTemplateOptions options)
    {
        var operation = options.Operation!;
        var command = BuiltinCliCatalog.Load().Get(operation.CommandId)
            ?? throw new ArgumentException("Unknown PowerToys operation.", nameof(options));
        if (options.Input != "none" || options.Output != "text" ||
            !string.IsNullOrWhiteSpace(options.FileExtensions) || options.MinFiles != 1 || options.MaxFiles != 0)
        {
            throw new ArgumentException("PowerToys operation templates have no primary input and return text. File inputs are declared parameters.", nameof(options));
        }

        var bindings = operation.Bindings is null
            ? CreateDefaultBindings(command)
            : operation.Bindings.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var parameters = options.Parameters ?? InferParameters(command, bindings);
        var action = new CliAction
        {
            SchemaVersion = 1,
            CommandId = command.Id,
            Bindings = bindings,
        };
        var inputSchema = CreateSchema("none");
        AddParameters(inputSchema, parameters);
        var descriptor = new JsonObject
        {
            // These placeholders permit validation before publishing any files. CreatePackage
            // assigns the independently generated identity and the user's title.
            ["name"] = "operation-template",
            ["title"] = command.Name,
            ["inputSchema"] = inputSchema,
            ["outputSchema"] = CreateSchema("text"),
            ["annotations"] = new JsonObject { ["readOnlyHint"] = true },
            ["x-execute"] = new JsonObject { ["provider"] = CliActionProvider.ProviderName },
            ["x-powerscript"] = new JsonObject
            {
                ["capabilities"] = JsonSerializer.SerializeToNode(command.RequiredCapabilities),
            },
        };
        var manifest = ToolDescriptorParser.Parse(descriptor.Deserialize<ToolDescriptor>(), "action.json")
            ?? throw new InvalidDataException("The operation template descriptor is invalid.");
        var errors = CliActionProvider.Validate(manifest, action);
        if (errors.Count != 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        }

        return CreatePackage(
            scriptsRoot,
            options.ProjectName,
            options.Description ?? command.Description,
            "action.json",
            descriptor,
            JsonSerializer.Serialize(action, ToolDescriptorParser.JsonOptions),
            CreateOperationGuide(command),
            "operation",
            iconPreset: options.IconPreset,
            iconSource: options.IconSource);
    }

    private static Dictionary<string, CliActionBinding> CreateDefaultBindings(BuiltinCliDefinition command) =>
        command.Parameters.ToDictionary(
            parameter => parameter.Name,
            parameter => parameter.Default is null
                ? new CliActionBinding { Parameter = parameter.Name }
                : new CliActionBinding { Constant = ParameterDefault(parameter) },
            StringComparer.Ordinal);

    private static IReadOnlyList<ScriptTemplateParameter> InferParameters(
        BuiltinCliDefinition command,
        IReadOnlyDictionary<string, CliActionBinding> bindings)
    {
        var parameters = new Dictionary<string, ScriptTemplateParameter>(StringComparer.Ordinal);
        foreach (var target in command.Parameters)
        {
            if (bindings.TryGetValue(target.Name, out var binding) && binding?.Parameter is { } name &&
                !parameters.ContainsKey(name))
            {
                parameters.Add(name, ToTemplateParameter(command, target) with { Name = name });
            }
        }

        return parameters.Values.ToArray();
    }

    private static ScriptTemplateParameter ToTemplateParameter(BuiltinCliDefinition command, ScriptParameter parameter)
    {
        var schema = new JsonSchemaProperty
        {
            Type = parameter.Type switch
            {
                ScriptParameter.ParameterTypeInt => "integer",
                ScriptParameter.ParameterTypeBool => "boolean",
                _ => "string",
            },
            Title = parameter.Label,
            Description = parameter.Description,
            Format = parameter.IsFile ? JsonSchemaProperty.FilePathFormat : null,
            Enum = parameter.IsChoice ? parameter.Options.ToList() : null,
            Minimum = parameter.Min,
            Maximum = parameter.Max,
            Default = parameter.Default is null ? default : ParameterDefault(parameter),
            Ui = command.ParameterUi.GetValueOrDefault(parameter.Name),
        };
        return new ScriptTemplateParameter(parameter.Name, schema, parameter.IsRequired);
    }

    private static JsonElement ParameterDefault(ScriptParameter parameter) => parameter.Type switch
    {
        ScriptParameter.ParameterTypeBool => JsonSerializer.SerializeToElement(bool.Parse(parameter.Default!)),
        ScriptParameter.ParameterTypeInt => JsonSerializer.SerializeToElement(int.Parse(parameter.Default!, CultureInfo.InvariantCulture)),
        _ => JsonSerializer.SerializeToElement(parameter.Default),
    };

    private static string CreateOperationGuide(BuiltinCliDefinition command) => $"""
        # {command.Name}

        This PowerScript was created from the built-in {command.Group} operation template.
        {command.Description}

        ## Package

        - action.json selects the known command and maps each argument to a fixed value or a declared runtime parameter.
        - action.json.tool.json declares the script's identity, parameters, text output and required capabilities.
        - The executable path, flags and argument order come from PowerToys' built-in catalogue.

        Fixed values and runtime defaults are different. A fixed value is always used; an exposed
        parameter's default is used only when its caller omitted that parameter. Consumers such as
        Keyboard Manager must supply required parameters before a shortcut can run without a form.

        Creation did not run the operation, approve its contents, change a hotkey or change MXC.
        Review trust before running. Editing either JSON file changes the approved content.
        Execution follows the global MXC sandbox settings, the module state and consumer policies;
        required capabilities are declarations, not permission grants. Missing deployment or an
        incompatible sandbox policy is reported at run time, with no direct-execution fallback.

        The result is text from the native command. Nonzero exit codes remain failures. Cancellation
        does not undo external effects, and Activity records invocation metadata rather than input
        paths, parameter values or command output.
        """;
}
