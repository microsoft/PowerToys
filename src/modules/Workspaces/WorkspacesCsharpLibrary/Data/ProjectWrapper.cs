// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WorkspacesCsharpLibrary.Data;

public struct ProjectWrapper
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("creation-time")]
    public long CreationTime { get; set; }

    [JsonPropertyName("last-launched-time")]
    public long LastLaunchedTime { get; set; }

    [JsonPropertyName("is-shortcut-needed")]
    public bool IsShortcutNeeded { get; set; }

    [JsonPropertyName("move-existing-windows")]
    public bool MoveExistingWindows { get; set; }

    [JsonPropertyName("monitor-configuration")]
    public List<MonitorConfigurationWrapper> MonitorConfiguration { get; set; }

    [JsonPropertyName("applications")]
    public List<ApplicationWrapper> Applications { get; set; }
}
