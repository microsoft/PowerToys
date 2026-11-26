// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using PowerScripts.Core.Execution;

namespace PowerScripts.Core.Manifest;

/// <summary>
/// A typed parameter contract exposed to consumer modules. Each consumer owns its parameter UI and
/// passes chosen values to the Host. The runtime restores declared boolean and integer types.
/// </summary>
public sealed class ScriptParameter
{
    public const string ParameterTypeString = "string";
    public const string ParameterTypeInt = "int";
    public const string ParameterTypeBool = "bool";
    public const string ParameterTypeChoice = "choice";
    public const string ParameterTypeFile = "file";

    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets one of: "string", "int", "bool", "choice", "file".</summary>
    public string Type { get; set; } = ParameterTypeString;

    /// <summary>Gets or sets optional consumer-facing display label; falls back to <see cref="Name"/>.</summary>
    public string? Label { get; set; }

    /// <summary>Gets or sets optional consumer-facing help text.</summary>
    public string? Description { get; set; }

    public string? Default { get; set; }

    public bool IsRequired { get; set; }

    /// <summary>Gets or sets allowed values for a <see cref="ParameterTypeChoice"/> parameter.</summary>
    public List<string> Options { get; set; } = new();

    public int? Min { get; set; }

    public int? Max { get; set; }

    /// <summary>Gets the display label to use in UI (label if set, otherwise the name).</summary>
    [JsonIgnore]
    public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? Name : Label!;

    /// <summary>Gets a value indicating whether true when <see cref="Type"/> is the choice type (case-insensitive).</summary>
    [JsonIgnore]
    public bool IsChoice => string.Equals(Type, ParameterTypeChoice, StringComparison.OrdinalIgnoreCase);

    /// <summary>Gets a value indicating whether true when <see cref="Type"/> is the bool type (case-insensitive).</summary>
    [JsonIgnore]
    public bool IsBool => string.Equals(Type, ParameterTypeBool, StringComparison.OrdinalIgnoreCase);

    /// <summary>Gets a value indicating whether true when <see cref="Type"/> is the int type (case-insensitive).</summary>
    [JsonIgnore]
    public bool IsInt => string.Equals(Type, ParameterTypeInt, StringComparison.OrdinalIgnoreCase);

    /// <summary>Gets a value indicating whether true when <see cref="Type"/> is the file type (case-insensitive).</summary>
    [JsonIgnore]
    public bool IsFile => string.Equals(Type, ParameterTypeFile, StringComparison.OrdinalIgnoreCase);
}
