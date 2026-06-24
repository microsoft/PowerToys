// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class OutGoingLanguageSettings
    {
        [JsonPropertyName("language")]
        public string LanguageTag { get; set; }

        public OutGoingLanguageSettings()
        {
        }

        public OutGoingLanguageSettings(string language)
        {
            LanguageTag = language;
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this, SettingsSerializationContext.Default.OutGoingLanguageSettings);
        }
    }
}
