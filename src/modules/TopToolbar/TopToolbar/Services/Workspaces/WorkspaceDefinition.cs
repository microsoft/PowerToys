// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TopToolbar.Services.Workspaces
{
    internal sealed class WorkspaceDefinition
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("creation-time")]
        public long CreationTime { get; set; }

        [JsonPropertyName("last-launched-time")]
        public long? LastLaunchedTime { get; set; }

        [JsonPropertyName("is-shortcut-needed")]
        public bool IsShortcutNeeded { get; set; }

        [JsonPropertyName("move-existing-windows")]
        public bool MoveExistingWindows { get; set; }

        [JsonPropertyName("monitor-configuration")]
        public List<MonitorDefinition> Monitors { get; set; } = new();

        [JsonPropertyName("applications")]
        public List<ApplicationDefinition> Applications { get; set; } = new();

        [JsonPropertyName("apps")]
        public List<ApplicationDefinition> LegacyApplications
        {
            get => Applications;
            set
            {
                if (value == null)
                {
                    return;
                }

                Applications = value;
            }
        }
    }
}
