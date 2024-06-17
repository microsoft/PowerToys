// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using AdvancedPaste.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace AdvancedPaste.Models
{
    internal sealed class CustomQuery : ISettingsConfig
    {
        public string Query { get; set; }

        public string ClipboardData { get; set; }

        public string GetModuleName() => Constants.AdvancedPasteModuleName;

        public string ToJsonString() => JsonSerializer.Serialize(this);

        public override string ToString()
            => JsonSerializer.Serialize(this);

        public bool UpgradeSettingsConfiguration() => false;
    }
}
