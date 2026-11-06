// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class SeamlessCursorSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "SeamlessCursor";

        [JsonPropertyName("properties")]
        public SeamlessCursorProperties Properties { get; set; }

        public SeamlessCursorSettings()
        {
            Name = ModuleName;
            Properties = new SeamlessCursorProperties();
            Version = "1.0";
        }

        public string GetModuleName() => Name;

        public ModuleType GetModuleType() => ModuleType.SeamlessCursor;

        public bool UpgradeSettingsConfiguration()
        {
            if (Properties == null)
            {
                Properties = new SeamlessCursorProperties();
                return true;
            }

            bool upgraded = false;
            if (Properties.StopAtEmptyEdges == null)
            {
                Properties.StopAtEmptyEdges = new BoolProperty(true);
                upgraded = true;
            }

            if (Properties.MapCursorWrap == null)
            {
                Properties.MapCursorWrap = new BoolProperty(true);
                upgraded = true;
            }

            return upgraded;
        }
    }
}
