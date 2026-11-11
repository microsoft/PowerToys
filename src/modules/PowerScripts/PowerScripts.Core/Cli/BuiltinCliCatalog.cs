// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Cli;

/// <summary>Installed, closed operation definitions shared by templates and their execution provider.</summary>
public sealed class BuiltinCliCatalog
{
    internal const int MaximumCatalogBytes = 256 * 1024;

    private const string ResourceName = "PowerScripts.Core.Cli.catalog.json";

    private BuiltinCliCatalog(IReadOnlyList<BuiltinCliDefinition> commands)
    {
        Commands = commands;
    }

    public IReadOnlyList<BuiltinCliDefinition> Commands { get; }

    /// <summary>The catalogue is part of the installed assembly; scripts cannot replace its launch recipes.</summary>
    public static BuiltinCliCatalog Load()
    {
        using var stream = typeof(BuiltinCliCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidDataException("The built-in PowerToys operation catalogue is unavailable.");
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true));
        return Parse(reader.ReadToEnd());
    }

    public BuiltinCliDefinition? Get(string id) => Commands.FirstOrDefault(command =>
        string.Equals(command.Id, id, StringComparison.OrdinalIgnoreCase));

    internal static BuiltinCliCatalog Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > MaximumCatalogBytes || Encoding.UTF8.GetByteCount(json) > MaximumCatalogBytes)
        {
            throw new InvalidDataException("The operation catalogue exceeds 256 KiB.");
        }

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            var root = document.RootElement;
            CheckMembers(root, "catalogue", "schemaVersion", "modules");
            if (!root.TryGetProperty("schemaVersion", out var version) || !version.TryGetInt32(out var number) || number != 2)
            {
                throw new InvalidDataException("The operation catalogue requires schemaVersion 2.");
            }

            var commands = new List<BuiltinCliDefinition>();
            var moduleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var module in RequiredArray(root, "modules").EnumerateArray())
            {
                CheckMembers(module, "module", "id", "title", "operations");
                var moduleId = RequiredText(module, "id");
                if (!IsIdentifier(moduleId) || !moduleIds.Add(moduleId))
                {
                    throw new InvalidDataException($"Invalid or duplicate module identifier '{moduleId}'.");
                }

                var title = RequiredText(module, "title");
                var operations = RequiredArray(module, "operations");
                if (operations.GetArrayLength() == 0)
                {
                    throw new InvalidDataException($"Module '{moduleId}' must declare at least one operation.");
                }

                foreach (var item in operations.EnumerateArray())
                {
                    var command = ReadCommand(item, moduleId, title);
                    if (!ids.Add(command.Id))
                    {
                        throw new InvalidDataException($"Duplicate operation '{command.Id}'.");
                    }

                    commands.Add(command);
                }
            }

            if (commands.Count == 0)
            {
                throw new InvalidDataException("The operation catalogue must declare at least one operation.");
            }

            return new BuiltinCliCatalog(commands.AsReadOnly());
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or OverflowException or KeyNotFoundException)
        {
            throw new InvalidDataException("The built-in operation catalogue is invalid.", ex);
        }
    }

    private static BuiltinCliDefinition ReadCommand(JsonElement item, string moduleId, string group)
    {
        CheckMembers(item, "operation", "id", "title", "description", "inputSchema", "x-execute",
            "requiredCapabilities", "requiresHostProcessVisibility", "requiresHostDesktop");
        var id = RequiredText(item, "id");
        if (!IsIdentifier(id))
        {
            throw new InvalidDataException("Operation identifiers may contain only letters, digits, '.', '_' or '-'.");
        }

        var parameters = ReadParameters(item.GetProperty("inputSchema"), out var parameterUi);
        var execute = item.GetProperty("x-execute");
        CheckMembers(execute, "x-execute", "command", "arguments");
        var tokens = ReadStrings(RequiredArray(execute, "command"), allowEmpty: true);
        if (tokens.Count == 0 || !CliExecutableResolver.IsValidRelativeExecutable(tokens[0]))
        {
            throw new InvalidDataException($"Operation '{id}' requires an installation-relative .exe path.");
        }

        var bindings = ReadBindings(RequiredArray(execute, "arguments"), parameters);
        var capabilities = ReadStrings(RequiredArray(item, "requiredCapabilities"));
        if (capabilities.Distinct(StringComparer.Ordinal).Count() != capabilities.Count)
        {
            throw new InvalidDataException($"Operation '{id}' declares duplicate capabilities.");
        }

        return new BuiltinCliDefinition
        {
            Id = id,
            ModuleId = moduleId,
            Name = RequiredText(item, "title"),
            Group = group,
            Description = RequiredText(item, "description"),
            Executable = tokens[0],
            FixedArguments = tokens.Skip(1).ToList().AsReadOnly(),
            Parameters = parameters,
            ParameterUi = parameterUi,
            Bindings = bindings,
            RequiredCapabilities = capabilities.AsReadOnly(),
            RequiresHostProcessVisibility = item.GetProperty("requiresHostProcessVisibility").GetBoolean(),
            RequiresHostDesktop = item.GetProperty("requiresHostDesktop").GetBoolean(),
        };
    }

    private static IReadOnlyList<ScriptParameter> ReadParameters(JsonElement input, out IReadOnlyDictionary<string, ParameterUiHints> parameterUi)
    {
        CheckMembers(input, "inputSchema", "type", "additionalProperties", "properties", "required");
        var properties = input.GetProperty("properties");
        if (RequiredText(input, "type") != "object" || input.GetProperty("additionalProperties").ValueKind != JsonValueKind.False ||
            properties.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Operation inputSchema must be a closed object with declared properties.");
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ui = new Dictionary<string, ParameterUiHints>(StringComparer.Ordinal);
        if (input.TryGetProperty("required", out _))
        {
            _ = ReadStrings(RequiredArray(input, "required"));
        }

        foreach (var property in properties.EnumerateObject())
        {
            if (!IsIdentifier(property.Name) || !names.Add(property.Name))
            {
                throw new InvalidDataException("Operation parameters require unique, portable names.");
            }

            CheckMembers(property.Value, property.Name, "type", "title", "description", "format", "enum", "default", "minimum", "maximum", "x-ui");
            var type = RequiredText(property.Value, "type");
            _ = RequiredText(property.Value, "title");
            if (property.Value.TryGetProperty("description", out _))
            {
                _ = RequiredText(property.Value, "description");
            }

            if (property.Value.TryGetProperty("format", out _))
            {
                _ = RequiredText(property.Value, "format");
            }

            if (property.Value.TryGetProperty("enum", out _))
            {
                _ = ReadStrings(RequiredArray(property.Value, "enum"), allowEmpty: true);
            }

            foreach (var bound in new[] { "minimum", "maximum" })
            {
                if (property.Value.TryGetProperty(bound, out var value) && !value.TryGetInt32(out _))
                {
                    throw new InvalidDataException($"Parameter '{property.Name}.{bound}' must be a 32-bit integer.");
                }
            }

            if (property.Value.TryGetProperty("x-ui", out var hints))
            {
                CheckMembers(hints, "x-ui", "control", "trueLabel", "falseLabel");
                var control = RequiredText(hints, "control");
                var trueLabel = RequiredText(hints, "trueLabel");
                var falseLabel = RequiredText(hints, "falseLabel");
                if (type != "boolean" || control != "choice" || string.Equals(trueLabel, falseLabel, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Parameter '{property.Name}.x-ui' requires a boolean choice with distinct labels.");
                }

                ui.Add(property.Name, new ParameterUiHints { Control = control, TrueLabel = trueLabel, FalseLabel = falseLabel });
            }
        }

        var schema = input.Deserialize<JsonSchemaObject>()!;
        ScriptSchema.ValidateDefinition(schema, "inputSchema");
        var parameters = new List<ScriptParameter>();
        foreach (var (name, property) in schema.Properties!)
        {
            if (property.Type is not ("string" or "integer" or "boolean") ||
                (property.Type != "integer" && (property.Minimum is not null || property.Maximum is not null)) ||
                (property.IsFileParameter && property.Enum is not null) ||
                (property.Enum is { } choices && (choices.Any(choice => choice is null || choice.Contains('\0')) ||
                    choices.Distinct(StringComparer.Ordinal).Count() != choices.Count)))
            {
                throw new InvalidDataException($"Unsupported operation parameter schema '{name}'.");
            }

            var parameter = new ScriptParameter
            {
                Name = name,
                Type = property.IsFileParameter ? ScriptParameter.ParameterTypeFile :
                    property.Enum is not null ? ScriptParameter.ParameterTypeChoice : property.Type switch
                    {
                        "integer" => ScriptParameter.ParameterTypeInt,
                        "boolean" => ScriptParameter.ParameterTypeBool,
                        _ => ScriptParameter.ParameterTypeString,
                    },
                Label = property.Title,
                Description = property.Description,
                Default = property.DefaultAsString(),
                IsRequired = schema.Required?.Contains(name, StringComparer.Ordinal) == true,
                Options = property.Enum ?? new(),
                Min = property.Minimum,
                Max = property.Maximum,
            };
            if (property.Default.ValueKind != JsonValueKind.Undefined &&
                !CliParameterResolver.TryReadConstant(parameter, property.Default, out _, out var error))
            {
                throw new InvalidDataException(error);
            }

            parameters.Add(parameter);
        }

        parameterUi = new ReadOnlyDictionary<string, ParameterUiHints>(ui);
        return parameters.AsReadOnly();
    }

    private static IReadOnlyList<CliArgumentBinding> ReadBindings(JsonElement arguments, IReadOnlyList<ScriptParameter> parameters)
    {
        var bindings = new List<CliArgumentBinding>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in arguments.EnumerateArray())
        {
            CheckMembers(item, "argument binding", "parameter", "kind", "flag", "attachedValue");
            var name = RequiredText(item, "parameter");
            var parameter = parameters.FirstOrDefault(parameter => parameter.Name == name);
            if (parameter is null || !seen.Add(name))
            {
                throw new InvalidDataException($"Unknown or duplicate argument parameter '{name}'.");
            }

            var kind = RequiredText(item, "kind") switch
            {
                "positional" => CliArgumentKind.Positional,
                "option" => CliArgumentKind.Option,
                "switch" => CliArgumentKind.Switch,
                _ => throw new InvalidDataException($"Unsupported argument kind for '{name}'."),
            };
            var flag = item.TryGetProperty("flag", out _) ? RequiredText(item, "flag") : null;
            var attached = item.TryGetProperty("attachedValue", out var attachedValue) && attachedValue.GetBoolean();
            if ((kind == CliArgumentKind.Positional && flag is not null) ||
                (kind != CliArgumentKind.Positional && (flag is null || flag.Length < 2 || flag[0] != '-' ||
                    flag.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)) || flag.Contains('='))) ||
                (kind == CliArgumentKind.Switch && !parameter.IsBool) ||
                (attached && kind != CliArgumentKind.Option))
            {
                throw new InvalidDataException($"Invalid argument binding for '{name}'.");
            }

            bindings.Add(new CliArgumentBinding { Parameter = name, Kind = kind, Name = flag, AttachedValue = attached });
        }

        if (seen.Count != parameters.Count)
        {
            throw new InvalidDataException("Every operation parameter must have exactly one argument binding.");
        }

        return bindings.AsReadOnly();
    }

    private static void CheckMembers(JsonElement value, string path, params string[] allowed)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"'{path}' must be an object.");
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in value.EnumerateObject())
        {
            if (!seen.Add(property.Name) || !allowed.Contains(property.Name, StringComparer.Ordinal))
            {
                throw new InvalidDataException($"Unknown or duplicate catalogue property '{path}.{property.Name}'.");
            }
        }
    }

    private static string RequiredText(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()) || property.GetString()!.Contains('\0'))
        {
            throw new InvalidDataException($"Catalogue property '{name}' must be a non-empty string without NUL characters.");
        }

        return property.GetString()!;
    }

    private static JsonElement RequiredArray(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"Catalogue property '{name}' must be an array.");
        }

        return property;
    }

    private static List<string> ReadStrings(JsonElement array, bool allowEmpty = false)
    {
        var result = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || item.GetString()!.Contains('\0') ||
                (!allowEmpty && string.IsNullOrWhiteSpace(item.GetString())))
            {
                throw new InvalidDataException("Catalogue arrays must contain valid string tokens.");
            }

            result.Add(item.GetString()!);
        }

        return result;
    }

    private static bool IsIdentifier(string value) => value.Length > 0 && value.All(character =>
        character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '_' or '-');
}
