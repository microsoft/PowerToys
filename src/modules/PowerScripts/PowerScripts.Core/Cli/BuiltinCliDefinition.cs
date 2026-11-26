// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Cli;

/// <summary>A known PowerToys operation offered by the script-template catalogue.</summary>
public sealed class BuiltinCliDefinition
{
    public string Id { get; init; } = string.Empty;

    public string ModuleId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Group { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Executable { get; init; } = string.Empty;

    public IReadOnlyList<string> FixedArguments { get; init; } = Array.Empty<string>();

    public IReadOnlyList<ScriptParameter> Parameters { get; init; } = Array.Empty<ScriptParameter>();

    /// <summary>Gets optional presentation hints, kept separate from the execution parameter contract.</summary>
    public IReadOnlyDictionary<string, ParameterUiHints> ParameterUi { get; init; } =
        new Dictionary<string, ParameterUiHints>();

    public IReadOnlyList<CliArgumentBinding> Bindings { get; init; } = Array.Empty<CliArgumentBinding>();

    public IReadOnlyList<string> RequiredCapabilities { get; init; } = Array.Empty<string>();

    /// <summary>Gets a value indicating whether the result depends on observing processes outside the script's sandbox.</summary>
    public bool RequiresHostProcessVisibility { get; init; }

    /// <summary>Gets a value indicating whether the operation contacts the running module through the host Windows desktop.</summary>
    public bool RequiresHostDesktop { get; init; }
}
