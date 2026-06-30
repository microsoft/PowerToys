// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class LowMemoryModuleSettings
    {
        [JsonPropertyName("TextExtractor")]
        public bool TextExtractor { get; set; }

        [JsonPropertyName("ColorPicker")]
        public bool ColorPicker { get; set; }

        [JsonPropertyName("AdvancedPaste")]
        public bool AdvancedPaste { get; set; }

        [JsonPropertyName("Peek")]
        public bool Peek { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JsonElement> AdditionalModules { get; set; } = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        public bool GetValue(string moduleKey, bool defaultValue = false)
        {
            return moduleKey switch
            {
                "TextExtractor" => TextExtractor,
                "ColorPicker" => ColorPicker,
                "AdvancedPaste" => AdvancedPaste,
                "Peek" => Peek,
                _ => AdditionalModules.TryGetValue(moduleKey, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : defaultValue,
            };
        }

        public bool SetValue(string moduleKey, bool value)
        {
            bool oldValue = GetValue(moduleKey);
            switch (moduleKey)
            {
                case "TextExtractor": TextExtractor = value; break;
                case "ColorPicker": ColorPicker = value; break;
                case "AdvancedPaste": AdvancedPaste = value; break;
                case "Peek": Peek = value; break;
                default: AdditionalModules[moduleKey] = JsonSerializer.SerializeToElement(value); break;
            }

            return oldValue != value;
        }

        public void EnsureValue(string moduleKey, bool defaultValue = false)
        {
            if (GetValue(moduleKey, defaultValue) != defaultValue || IsKnownModule(moduleKey) || AdditionalModules.ContainsKey(moduleKey))
            {
                return;
            }

            AdditionalModules[moduleKey] = JsonSerializer.SerializeToElement(defaultValue);
        }

        private static bool IsKnownModule(string moduleKey) => moduleKey is "TextExtractor" or "ColorPicker" or "AdvancedPaste" or "Peek";
    }
}
