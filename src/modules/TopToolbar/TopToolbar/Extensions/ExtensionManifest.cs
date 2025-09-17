// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace TopToolbar.Extensions
{
    public sealed class ExtensionManifest
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("runtime")]
        public string Runtime { get; set; } = string.Empty;

        [JsonPropertyName("entry")]
        public string Entry { get; set; } = string.Empty;

        [JsonPropertyName("contributes")]
        public ExtensionContributions Contributes { get; set; } = new ExtensionContributions();

        [JsonPropertyName("permissions")]
        public System.Collections.Generic.List<string> Permissions { get; set; } = new System.Collections.Generic.List<string>();
    }
}
