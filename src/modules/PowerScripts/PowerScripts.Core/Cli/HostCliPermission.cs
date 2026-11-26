// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PowerScripts.Core.Execution;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Security;
using PowerScripts.Core.Templates;

namespace PowerScripts.Core.Cli;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class HostCliPermission
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("bindings")]
    public IReadOnlyDictionary<string, PowerShellCliBinding> Bindings { get; set; } =
        new Dictionary<string, PowerShellCliBinding>();
}
