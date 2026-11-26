// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Cli;

/// <summary>Argument placement belongs to the installed catalogue, never the editable action.</summary>
public sealed class CliArgumentBinding
{
    public string Parameter { get; init; } = string.Empty;

    public CliArgumentKind Kind { get; init; }

    public string? Name { get; init; }

    public bool AttachedValue { get; init; }
}
