// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using PowerScripts.Core.Cli;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Templates;

public sealed record PowerShellCliTemplate(
    string Id,
    string Name,
    string Description,
    string Input,
    string Output,
    IReadOnlyList<ScriptTemplateParameter> Parameters,
    [property: JsonIgnore] string Executable,
    [property: JsonIgnore] string Arguments,
    ScriptTemplateParameter? InputArgument = null,
    string? HostExecutionNotice = null,
    [property: JsonIgnore] string? StandardInput = null,
    [property: JsonIgnore] string? OutputScript = null)
{
    public string ModuleId { get; init; } = string.Empty;

    public bool SupportsFileBatch { get; init; }

    public string Group { get; init; } = string.Empty;

    public string IconName { get; init; } = "PowerToys";

    internal string CommandSynopsis { get; init; } = string.Empty;

    internal bool WritesState { get; init; }

    internal bool UsesNetwork { get; init; }
}
