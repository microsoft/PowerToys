// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class McpSettings : BasePTModuleSettings, ISettingsConfig, ICloneable
    {
        public const string ModuleName = "MCP";

        public McpSettings()
        {
            Name = ModuleName;
            Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Properties = new McpProperties();
        }

        [JsonPropertyName("properties")]
        public McpProperties Properties { get; set; }

        public object Clone()
        {
            return new McpSettings()
            {
                Name = Name,
                Version = Version,
                Properties = new McpProperties()
                {
                    RegisterToVSCode = Properties.RegisterToVSCode,
                    RegisterToWindowsCopilot = Properties.RegisterToWindowsCopilot,
                    EnabledModules = Properties.EnabledModules.ToDictionary(entry => entry.Key, entry => entry.Value),
                },
            };
        }

        public string GetModuleName()
        {
            return Name;
        }

        public bool UpgradeSettingsConfiguration()
        {
            return false;
        }
    }
}
