// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PowerScripts.Core.Cli;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Registry;

namespace PowerScripts.Core.Templates;

public sealed record ScriptTemplateOptions(
    string ProjectName,
    string? Description = null,
    string? FileExtensions = null,
    string Runtime = "powershell",
    string Input = "none",
    string Output = "text",
    int MinFiles = 1,
    int MaxFiles = 0,
    IReadOnlyList<ScriptTemplateParameter>? Parameters = null,
    string? IconPreset = null,
    string? IconSource = null,
    PowerToysOperationTemplateOptions? Operation = null,
    string? CliContent = null,
    IReadOnlyDictionary<string, PowerShellCliBinding>? CliBindings = null);
