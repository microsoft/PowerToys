// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace PowerScripts.PromptUI;

/// <summary>
/// The prompt description handed to this helper by <c>PowerScripts.Host</c> via a temp JSON file.
/// The shape is intentionally simple and self-contained (no reference to the Core manifest types) so
/// the helper stays decoupled from the rest of the prototype.
/// </summary>
public sealed class PromptSpec
{
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<PromptParam> Parameters { get; set; } = new();
}

/// <summary>A single parameter to collect. <see cref="Value"/> is the pre-filled (default/override) value.</summary>
public sealed class PromptParam
{
    public string Name { get; set; } = string.Empty;

    /// <summary>One of: string, int, bool, choice.</summary>
    public string Type { get; set; } = "string";

    public string? Label { get; set; }

    public string? Description { get; set; }

    public List<string>? Options { get; set; }

    public string? Value { get; set; }

    public int? Min { get; set; }

    public int? Max { get; set; }

    public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? Name : Label!;
}
