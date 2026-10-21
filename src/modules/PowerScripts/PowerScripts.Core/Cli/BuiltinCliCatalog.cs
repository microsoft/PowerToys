// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Cli;

/// <summary>Loads only the command definitions embedded in the installed Core assembly.</summary>
public sealed class BuiltinCliCatalog
{
    private const string ResourceName = "PowerScripts.BuiltinCliCatalog.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter<CliArgumentKind>(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private BuiltinCliCatalog(IReadOnlyList<BuiltinCliDefinition> commands)
    {
        Commands = commands;
    }

    public IReadOnlyList<BuiltinCliDefinition> Commands { get; }

    public static BuiltinCliCatalog Load()
    {
        using var stream = typeof(BuiltinCliCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidDataException("The built-in CLI catalogue is missing from this installation.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    public BuiltinCliDefinition? Get(string id) => Commands.FirstOrDefault(command =>
        string.Equals(command.Id, id, StringComparison.OrdinalIgnoreCase));

    internal static BuiltinCliCatalog Parse(string json)
    {
        CatalogDocument document;
        try
        {
            document = JsonSerializer.Deserialize<CatalogDocument>(json, JsonOptions)
                ?? throw new InvalidDataException("The built-in CLI catalogue must be an object.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The built-in CLI catalogue is invalid.", ex);
        }

        if (document.SchemaVersion != 1 || document.Commands is not { Count: > 0 })
        {
            throw new InvalidDataException("The built-in CLI catalogue requires schemaVersion 1 and commands.");
        }

        var commands = new List<BuiltinCliDefinition>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in document.Commands)
        {
            if (entry is null || !IsIdentifier(entry.Id) || string.IsNullOrWhiteSpace(entry.Title) ||
                string.IsNullOrWhiteSpace(entry.Group) || !ids.Add(entry.Id))
            {
                throw new InvalidDataException("Built-in CLI commands require unique IDs, titles, and groups.");
            }

            if (entry.Execute?.Command is not { Count: > 0 } command ||
                !CliExecutableResolver.IsValidRelativeExecutable(command[0]) ||
                command.Any(token => token is null || token.Contains('\0')))
            {
                throw new InvalidDataException($"Command '{entry.Id}' must name a relative .exe inside the installation.");
            }

            var parameters = ReadParameters(entry);
            var bindings = entry.Execute.Arguments ?? new List<CliArgumentBinding>();
            var bound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var binding in bindings)
            {
                var parameter = binding is null ? null : parameters.FirstOrDefault(parameter =>
                    string.Equals(parameter.Name, binding.Parameter, StringComparison.OrdinalIgnoreCase));
                if (parameter is null || !bound.Add(parameter.Name) || !Enum.IsDefined(binding!.Kind))
                {
                    throw new InvalidDataException($"Command '{entry.Id}' has an invalid or duplicate argument binding.");
                }

                if (binding.Kind == CliArgumentKind.Positional)
                {
                    if (!string.IsNullOrEmpty(binding.Name))
                    {
                        throw new InvalidDataException("A positional argument cannot declare a flag.");
                    }
                }
                else
                {
                    var flag = binding.Name;
                    if (string.IsNullOrEmpty(flag) || flag.Length < 2 || flag == "--" || flag[0] != '-' ||
                        flag.Any(character => char.IsWhiteSpace(character) || character == '\0'))
                    {
                        throw new InvalidDataException("Options and switches require a single flag token.");
                    }
                }

                if (binding.Kind == CliArgumentKind.Switch && !parameter.IsBool)
                {
                    throw new InvalidDataException("Switch bindings require a boolean parameter.");
                }
            }

            if (bound.Count != parameters.Count)
            {
                throw new InvalidDataException($"Command '{entry.Id}' must bind every declared parameter.");
            }

            commands.Add(new BuiltinCliDefinition
            {
                Id = entry.Id,
                Name = entry.Title,
                Group = entry.Group,
                Description = entry.Description ?? string.Empty,
                Executable = command[0],
                FixedArguments = command.Skip(1).ToArray(),
                Parameters = parameters.AsReadOnly(),
                Bindings = bindings.AsReadOnly(),
            });
        }

        return new BuiltinCliCatalog(commands.AsReadOnly());
    }

    private static List<ScriptParameter> ReadParameters(CatalogEntry entry)
    {
        var schema = entry.InputSchema;
        if (schema is null || schema.Type != "object")
        {
            throw new InvalidDataException($"Command '{entry.Id}' requires an object inputSchema.");
        }

        var properties = schema.Properties ?? new Dictionary<string, ParameterSchema>();
        var required = schema.Required ?? new List<string>();
        if (required.Distinct(StringComparer.Ordinal).Count() != required.Count ||
            required.Any(name => string.IsNullOrEmpty(name) || !properties.ContainsKey(name)))
        {
            throw new InvalidDataException("Required parameters must refer to declared properties.");
        }

        var parameters = new List<ScriptParameter>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, property) in properties)
        {
            if (!IsIdentifier(name) || !names.Add(name) || property is null)
            {
                throw new InvalidDataException("Parameter names must be unique identifiers.");
            }

            var type = property.Type switch
            {
                "string" when property.Format == "file-path" => ScriptParameter.ParameterTypeFile,
                "string" when property.Enum is { Count: > 0 } => ScriptParameter.ParameterTypeChoice,
                "string" => ScriptParameter.ParameterTypeString,
                "integer" => ScriptParameter.ParameterTypeInt,
                "boolean" => ScriptParameter.ParameterTypeBool,
                _ => throw new InvalidDataException($"Parameter '{name}' has an unsupported schema type."),
            };
            if ((!string.IsNullOrEmpty(property.Format) && (property.Type != "string" || property.Format != "file-path")) ||
                (property.Format == "file-path" && property.Enum is not null) ||
                (property.Enum is not null && (property.Type != "string" || property.Enum.Count == 0 ||
                 property.Enum.Any(string.IsNullOrEmpty) || property.Enum.Distinct(StringComparer.Ordinal).Count() != property.Enum.Count)) ||
                (property.Minimum.HasValue && property.Maximum.HasValue && property.Maximum.Value < property.Minimum.Value) ||
                ((property.Minimum.HasValue || property.Maximum.HasValue) && type != ScriptParameter.ParameterTypeInt))
            {
                throw new InvalidDataException($"Parameter '{name}' has invalid constraints.");
            }

            var parameter = new ScriptParameter
            {
                Name = name,
                Type = type,
                Label = property.Title,
                Description = property.Description,
                IsRequired = required.Contains(name, StringComparer.Ordinal),
                Default = ReadDefault(property),
                Options = property.Enum ?? new List<string>(),
                Min = property.Minimum,
                Max = property.Maximum,
            };
            if (parameter.Default is { } value && !CliParameterResolver.TryValidate(parameter, value, out _, out var error))
            {
                throw new InvalidDataException(error);
            }

            parameters.Add(parameter);
        }

        return parameters;
    }

    private static string? ReadDefault(ParameterSchema schema)
    {
        if (schema.Default is not { } value || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return (schema.Type, value.ValueKind) switch
        {
            ("string", JsonValueKind.String) => value.GetString(),
            ("integer", JsonValueKind.Number) when value.TryGetInt32(out _) => value.GetRawText(),
            ("boolean", JsonValueKind.True) => "true",
            ("boolean", JsonValueKind.False) => "false",
            _ => throw new InvalidDataException("A parameter default must match its schema type."),
        };
    }

    private static bool IsIdentifier(string? value) => !string.IsNullOrWhiteSpace(value) &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');

    private sealed class CatalogDocument
    {
        public CatalogDocument()
        {
        }

        public int SchemaVersion { get; init; }

        public List<CatalogEntry>? Commands { get; init; }
    }

    private sealed class CatalogEntry
    {
        public CatalogEntry()
        {
        }

        public string Id { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string Group { get; init; } = string.Empty;

        public string? Description { get; init; }

        public InputSchema? InputSchema { get; init; }

        [JsonPropertyName("x-execute")]
        public ExecuteSchema? Execute { get; init; }
    }

    private sealed class InputSchema
    {
        public InputSchema()
        {
        }

        public string? Type { get; init; }

        public Dictionary<string, ParameterSchema>? Properties { get; init; }

        public List<string>? Required { get; init; }
    }

    private sealed class ParameterSchema
    {
        public ParameterSchema()
        {
        }

        public string? Type { get; init; }

        public string? Format { get; init; }

        public string? Title { get; init; }

        public string? Description { get; init; }

        public JsonElement? Default { get; init; }

        public List<string>? Enum { get; init; }

        public int? Minimum { get; init; }

        public int? Maximum { get; init; }
    }

    private sealed class ExecuteSchema
    {
        public ExecuteSchema()
        {
        }

        public List<string>? Command { get; init; }

        public List<CliArgumentBinding>? Arguments { get; init; }
    }
}
