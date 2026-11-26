// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using PowerScripts.Core.Cli;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Templates;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PowerShellCliBinding(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] JsonElement Value = default);
