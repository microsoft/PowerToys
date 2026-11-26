// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace PowerScripts.Client;

/// <summary>A typed parameter contract that a consumer module may render using its own UX.</summary>
public sealed record PowerScriptParameter(
    string Name,
    string Type,
    bool IsRequired,
    string? Label,
    string? Description,
    string? Default,
    IReadOnlyList<string> Options,
    int? Min,
    int? Max);
