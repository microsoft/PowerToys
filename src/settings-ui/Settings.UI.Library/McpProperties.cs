// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class McpProperties
    {
        public McpProperties()
        {
            RegisterToVSCode = false;
            RegisterToWindowsCopilot = false;
            EnabledModules = new Dictionary<string, bool>
            {
                { "Awake", true },
            };
        }

        [JsonPropertyName("registerToVSCode")]
        public bool RegisterToVSCode { get; set; }

        [JsonPropertyName("registerToWindowsCopilot")]
        public bool RegisterToWindowsCopilot { get; set; }

        [JsonPropertyName("enabledModules")]
        public Dictionary<string, bool> EnabledModules { get; set; }
    }
}
