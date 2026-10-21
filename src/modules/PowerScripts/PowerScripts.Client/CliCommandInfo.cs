// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Client;

/// <summary>A built-in CLI command reported by the PowerScripts host.</summary>
public sealed class CliCommandInfo
{
    /// <summary>Stable identity passed to <c>cli run</c>.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Human-readable command name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Human-readable group, such as the module that provides the command.</summary>
    public string Group { get; init; } = string.Empty;

    /// <summary>Description of the command's behavior.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Whether the host can currently execute the command.</summary>
    public bool IsAvailable { get; init; }

    /// <summary>Diagnostic explaining why the command is unavailable.</summary>
    public string? UnavailableReason { get; init; }

    /// <summary>Typed parameters to collect before running the command.</summary>
    public IReadOnlyList<PowerScriptParameter> Parameters { get; init; } = [];

    internal static bool IsValidId(string? id) =>
        !string.IsNullOrEmpty(id) &&
        id.All(character =>
            character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '_' or '-');
}
