// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.MacroCommon.Models;

public sealed record MacroDefinition
{
    public string Id { get; init; } = Guid.NewGuid().ToString();

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? Hotkey { get; init; }

    public string? AppScope { get; init; }

    public List<MacroStep> Steps { get; init; } = [];
}
