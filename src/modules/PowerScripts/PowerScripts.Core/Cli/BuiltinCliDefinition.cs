// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Cli;

/// <summary>How a declared parameter is represented in the native command's argv.</summary>
public enum CliArgumentKind
{
    Positional,
    Option,
    Switch,
}

/// <summary>A declarative argv binding; parameter values never become shell command text.</summary>
public sealed class CliArgumentBinding
{
    public string Parameter { get; init; } = string.Empty;

    public CliArgumentKind Kind { get; init; }

    [JsonPropertyName("flag")]
    public string? Name { get; init; }
}

/// <summary>A command shipped in PowerToys' embedded catalogue, independent of script manifests.</summary>
public sealed class BuiltinCliDefinition
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Group { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    /// <summary>Executable path relative to the trusted PowerToys deployment directory.</summary>
    public string Executable { get; init; } = string.Empty;

    public IReadOnlyList<string> FixedArguments { get; init; } = Array.Empty<string>();

    public IReadOnlyList<ScriptParameter> Parameters { get; init; } = Array.Empty<ScriptParameter>();

    public IReadOnlyList<CliArgumentBinding> Bindings { get; init; } = Array.Empty<CliArgumentBinding>();

    public bool TryResolveParameters(
        IReadOnlyDictionary<string, string?> supplied,
        out Dictionary<string, string?> resolved,
        out string error) => CliParameterResolver.TryResolve(this, supplied, out resolved, out error);
}
