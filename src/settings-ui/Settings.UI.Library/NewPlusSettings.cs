// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Settings.UI.Library.Resources;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class NewPlusSettings : ISettingsConfig
    {
        public const string ModuleName = "NewPlus";

        public void InitializeWithDefaultSettings()
        {
            // This code path should never happen
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

        [JsonPropertyName("HideFileExtension")]
        public bool HideFileExtension { get; set; }

        [JsonPropertyName("HideStartingDigits")]
        public bool HideStartingDigits { get; set; }

        [JsonPropertyName("TemplateLocation")]
        public string TemplateLocation { get; set; }

        public string GetModuleName()
        {
            return ModuleName;
        }

        public bool UpgradeSettingsConfiguration()
        {
            return false;
        }
    }
}
