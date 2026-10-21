// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace PowerScripts.Client;

/// <summary>The result of requesting the host's built-in CLI command catalog.</summary>
public sealed class CliCatalogResult
{
    /// <summary>Whether the host successfully returned its catalog.</summary>
    public bool Succeeded { get; init; }

    /// <summary>Host or protocol diagnostic when the catalog could not be retrieved.</summary>
    public string? Error { get; init; }

    /// <summary>The commands in the built-in catalog.</summary>
    public IReadOnlyList<CliCommandInfo> Commands { get; init; } = [];

    /// <summary>
    /// Parses a <c>cli list --json</c> response without reflection. Invalid responses return an
    /// explicit failure so consumers can distinguish a catalog failure from an empty catalog.
    /// </summary>
    public static CliCatalogResult Parse(string json, int hostExitCode = PowerScriptsProtocol.ExitCode.Ok)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return InvalidResponse();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("succeeded", out var succeeded) ||
                succeeded.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return InvalidResponse();
            }

            var error = ReadOptionalString(root, "error");
            if (succeeded.ValueKind == JsonValueKind.False)
            {
                return new CliCatalogResult
                {
                    Error = string.IsNullOrWhiteSpace(error) ? "The PowerScripts host could not return its CLI catalog." : error,
                };
            }

            if (hostExitCode != PowerScriptsProtocol.ExitCode.Ok)
            {
                return InvalidResponse();
            }

            if (!root.TryGetProperty("commands", out var commands) || commands.ValueKind != JsonValueKind.Array)
            {
                return InvalidResponse();
            }

            var results = new List<CliCommandInfo>();
            var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var command in commands.EnumerateArray())
            {
                if (command.ValueKind != JsonValueKind.Object ||
                    !TryReadString(command, "id", out var id) ||
                    !CliCommandInfo.IsValidId(id) ||
                    !identities.Add(id) ||
                    !TryReadString(command, "name", out var name) ||
                    !TryReadString(command, "group", out var group) ||
                    !TryReadString(command, "description", out var description) ||
                    !command.TryGetProperty("isAvailable", out var available) ||
                    available.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                    !HasValidParameters(command))
                {
                    return InvalidResponse();
                }

                results.Add(new CliCommandInfo
                {
                    Id = id,
                    Name = name,
                    Group = group,
                    Description = description,
                    IsAvailable = available.ValueKind == JsonValueKind.True,
                    UnavailableReason = ReadOptionalString(command, "unavailableReason"),
                    Parameters = PowerScriptInfo.ParseParameters(command),
                });
            }

            return new CliCatalogResult { Succeeded = true, Error = error, Commands = results };
        }
        catch (JsonException)
        {
            return InvalidResponse();
        }
    }

    private static CliCatalogResult InvalidResponse() => new()
    {
        Error = "The PowerScripts host returned an invalid CLI catalog response.",
    };

    private static bool HasValidParameters(JsonElement command)
    {
        if (!command.TryGetProperty("parameters", out var parameters) || parameters.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in parameters.EnumerateArray())
        {
            if (parameter.ValueKind != JsonValueKind.Object ||
                !TryReadString(parameter, "name", out var name) ||
                string.IsNullOrWhiteSpace(name) ||
                name.Contains('\0') ||
                !names.Add(name) ||
                !TryReadString(parameter, "type", out var type) ||
                type is not ("string" or "int" or "bool" or "choice" or "file") ||
                !parameter.TryGetProperty("isRequired", out var isRequired) ||
                isRequired.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                !parameter.TryGetProperty("options", out var options) ||
                options.ValueKind != JsonValueKind.Array ||
                options.EnumerateArray().Any(option => option.ValueKind != JsonValueKind.String))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadString(JsonElement element, string name, out string value)
    {
        if (element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static string? ReadOptionalString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
