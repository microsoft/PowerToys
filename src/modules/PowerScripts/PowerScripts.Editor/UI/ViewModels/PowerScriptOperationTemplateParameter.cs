// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace PowerScripts.UI.ViewModels;

public sealed class PowerScriptOperationTemplateParameter
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("schema")]
    public JsonObject Schema { get; set; }

    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [JsonPropertyName("fileExtensions")]
    public string FileExtensions { get; set; }
}
