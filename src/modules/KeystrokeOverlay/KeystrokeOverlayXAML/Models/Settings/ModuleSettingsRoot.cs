// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace KeystrokeOverlayUI.Models
{
    public class ModuleSettingsRoot
    {
        [JsonPropertyName("properties")]
        public ModuleProperties Properties { get; set; } = new();
    }
}
