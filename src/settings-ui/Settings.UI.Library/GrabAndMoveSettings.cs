// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using System.Text.Json.Serialization;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class GrabAndMoveSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "GrabAndMove";

        public GrabAndMoveSettings()
        {
            Name = ModuleName;
            Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Properties = new GrabAndMoveProperties();
        }

        [JsonPropertyName("properties")]
        public GrabAndMoveProperties Properties { get; set; }

        public string GetModuleName() => Name;

        public bool UpgradeSettingsConfiguration() => false;

        public ModuleType GetModuleType() => ModuleType.GrabAndMove;
    }
}
