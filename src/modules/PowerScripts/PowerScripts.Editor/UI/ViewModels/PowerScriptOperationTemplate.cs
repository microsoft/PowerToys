// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PowerScripts.UI.ViewModels;

/// <summary>Creation metadata supplied by the Host's existing template catalogue.</summary>
public sealed class PowerScriptOperationTemplate
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("group")]
    public string Group { get; set; }

    [JsonPropertyName("moduleId")]
    public string ModuleId { get; set; }

    [JsonPropertyName("commandId")]
    public string CommandId { get; set; }

    [JsonPropertyName("unavailableReason")]
    public string UnavailableReason { get; set; }

    [JsonPropertyName("parameters")]
    public List<PowerScriptOperationTemplateParameter> Parameters { get; set; } = new();

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Group) ? Name : Group + " — " + Name;
}
