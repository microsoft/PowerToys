// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class TextExpansionDataModel
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("sourceText")]
        public string SourceText { get; set; } = string.Empty;

        [JsonPropertyName("activationKeys")]
        public List<uint> ActivationKeys { get; set; } = new List<uint>();

        [JsonPropertyName("replacementText")]
        public string ReplacementText { get; set; } = string.Empty;

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;
    }
}
